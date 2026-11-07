using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// เมนู QR สั่งอาหาร = Product ที่ติ๊ก Dining_Menu_Item.Is_Dining + หมวด/ชื่อแสดง/คำอธิบาย/sold-out/โซนที่ขาย/ตัวเลือก
    /// รูปจาก Product_Images (ProductType='PRODUCT', Status=1 — รูปหลัก IsMainImage ก่อน) · ราคา = Product.Sell_Price (รวม VAT)
    /// สต๊อก (เฉพาะ Track_Stock) = ΣProduct_In − ΣProduct_Out (สูตรเดียวกับ Guest/RoomService.aspx.cs:127-154)
    /// cache ทั้งเมนู 60 วินาที (ทุกโซนใช้ชุดเดียว กรองตอนอ่าน) — หน้าแอดมินบันทึกแล้วเรียก <see cref="Invalidate"/>
    /// </summary>
    public class DiningMenuService
    {
        private readonly string _conn;
        private static readonly object _lock = new object();
        private static DiningMenu _cache;
        private static DateTime _cacheAt = DateTime.MinValue;
        private const int CacheSec = 60;

        public DiningMenuService() : this(DiningDb.ConnStr) { }
        public DiningMenuService(string connectionString) { _conn = connectionString; }

        public static void Invalidate()
        {
            lock (_lock) { _cache = null; _cacheAt = DateTime.MinValue; }
        }

        // ── อ่านเมนู (หน้าลูกค้า / ตรวจราคา) ──────────────────────────────────

        /// <summary>เมนูของโซน: รายการที่ Is_Dining + Product ใช้งาน + อยู่ในโซน (รายการ sold-out ยังอยู่ — หน้าแสดงป้าย "หมด")</summary>
        public DiningMenu GetMenu(string zoneCode)
        {
            var all = LoadAll();
            var m = new DiningMenu { ZoneCode = zoneCode, LoadedAt = all.LoadedAt };
            var counts = new Dictionary<int, int>();
            foreach (var it in all.Items)
            {
                if (!it.IsDining || !it.ProductActive || !it.InZone(zoneCode)) continue;
                m.Items.Add(it);
                int cid = it.CategoryId ?? 0;
                int c; counts.TryGetValue(cid, out c); counts[cid] = c + 1;
            }
            foreach (var c in all.Categories)
            {
                if (!c.IsActive) continue;
                int n; counts.TryGetValue(c.Id, out n);
                var copy = new DiningMenuCategory { Id = c.Id, NameTh = c.NameTh, NameEn = c.NameEn, Icon = c.Icon, SortOrder = c.SortOrder, IsActive = c.IsActive, ItemCount = n };
                if (n > 0) m.Categories.Add(copy);
            }
            int uncategorized; counts.TryGetValue(0, out uncategorized);
            if (uncategorized > 0) m.Categories.Add(new DiningMenuCategory { Id = 0, NameTh = "อื่น ๆ", SortOrder = int.MaxValue, ItemCount = uncategorized });
            return m;
        }

        /// <summary>รายการเดียว (จาก cache) — null = ไม่มีในเมนู</summary>
        public DiningMenuItem GetItem(int productId)
        {
            foreach (var it in LoadAll().Items) if (it.ProductId == productId) return it;
            return null;
        }

        /// <summary>ตัวค้นสำหรับ DiningPricing.ComputeQuote (ตรวจ Is_Dining/sold-out/โซน/สต๊อกใน ComputeQuote เอง)</summary>
        public Func<int, DiningMenuItem> Lookup()
        {
            var all = LoadAll();
            var map = new Dictionary<int, DiningMenuItem>();
            foreach (var it in all.Items) map[it.ProductId] = it;
            return delegate (int pid)
            {
                DiningMenuItem it;
                return map.TryGetValue(pid, out it) ? it : null;
            };
        }

        /// <summary>สต๊อกสดของสินค้า (ไม่ผ่าน cache) — ใช้ตอนยืนยันสั่งรายการ Track_Stock</summary>
        public decimal LiveStock(int productId)
        {
            try
            {
                object o = DiningDb.Scalar(_conn,
                    @"SELECT ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = @p), 0) - ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = @p), 0)",
                    new Dictionary<string, object> { { "@p", productId } });
                return DiningDb.Dec(o);
            }
            catch { return 0m; }
        }

        private DiningMenu LoadAll()
        {
            lock (_lock)
            {
                if (_cache != null && (DateTime.UtcNow - _cacheAt).TotalSeconds < CacheSec) return _cache;
            }
            var m = new DiningMenu { LoadedAt = DateTime.Now };
            try
            {
                if (DiningDb.Ready(_conn))
                {
                    m.Categories = ListCategories(true);
                    var groups = ListOptionGroups(true);
                    var gmap = new Dictionary<int, DiningOptionGroup>();
                    foreach (var g in groups) gmap[g.Id] = g;

                    bool hasImages = DiningDb.TableExists(_conn, "Product_Images");
                    string imgSql = hasImages
                        ? @"OUTER APPLY (SELECT TOP 1 i.ImageURL, i.ThumbnailURL FROM Product_Images i
                                        WHERE i.Product_ID = p.ID AND i.ProductType = 'PRODUCT' AND i.Status = 1
                                        ORDER BY i.IsMainImage DESC, i.ImageOrder, i.ID) img"
                        : "OUTER APPLY (SELECT CAST(NULL AS NVARCHAR(500)) AS ImageURL, CAST(NULL AS NVARCHAR(500)) AS ThumbnailURL) img";

                    var dt = DiningDb.Query(_conn, @"
                        SELECT p.ID, p.Product_Name, p.Sell_Price, p.Status, p.Barcode,
                               ISNULL(p.Cost_Price, 0) AS Cost_Price,
                               m.Is_Dining, m.Category_ID, m.Display_Name_TH, m.Display_Name_EN, m.Description_TH, m.Is_Sold_Out, m.Sold_Out_Until,
                               m.Prep_Minutes, m.Allergen_Tags, m.Sort_Order, m.Available_Zones, m.Track_Stock, m.Is_Recommended,
                               c.Name_TH AS Category_Name,
                               CASE WHEN m.Track_Stock = 1 THEN
                                    ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                                    ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0) END AS Stock_Qty,
                               img.ImageURL, img.ThumbnailURL
                          FROM dbo.Dining_Menu_Item m
                          JOIN Product p ON p.ID = m.Product_ID
                          LEFT JOIN dbo.Dining_Menu_Category c ON c.ID = m.Category_ID
                          " + imgSql + @"
                         ORDER BY ISNULL(c.Sort_Order, 2147483647), m.Sort_Order, p.Product_Name");
                    var items = new Dictionary<int, DiningMenuItem>();
                    foreach (DataRow r in dt.Rows)
                    {
                        var it = new DiningMenuItem
                        {
                            ProductId = DiningDb.Int(r["ID"]),
                            ProductName = DiningDb.Str(r["Product_Name"]) ?? "",
                            Price = DiningDb.Dec(r["Sell_Price"]),
                            CostPrice = DiningDb.Dec(r["Cost_Price"]),
                            Barcode = DiningDb.Str(r["Barcode"]),
                            ProductActive = IsActiveStatus(r["Status"]),
                            IsDining = DiningDb.Bool(r["Is_Dining"]),
                            CategoryId = DiningDb.NInt(r["Category_ID"]),
                            CategoryName = DiningDb.Str(r["Category_Name"]),
                            NameEn = DiningDb.Str(r["Display_Name_EN"]),
                            Description = DiningDb.Str(r["Description_TH"]),
                            IsSoldOut = DiningDb.Bool(r["Is_Sold_Out"]),
                            SoldOutUntil = DiningDb.NDate(r["Sold_Out_Until"]),
                            PrepMinutes = DiningDb.NInt(r["Prep_Minutes"]),
                            AllergenTags = DiningDb.Str(r["Allergen_Tags"]),
                            SortOrder = DiningDb.Int(r["Sort_Order"]),
                            AvailableZones = DiningDb.Str(r["Available_Zones"]),
                            TrackStock = DiningDb.Bool(r["Track_Stock"]),
                            StockQty = DiningDb.NDec(r["Stock_Qty"]),
                            IsRecommended = DiningDb.Bool(r["Is_Recommended"]),
                            ImageUrl = DiningDb.Str(r["ImageURL"]),
                            ThumbUrl = DiningDb.Str(r["ThumbnailURL"])
                        };
                        string disp = DiningDb.Str(r["Display_Name_TH"]);
                        it.Name = string.IsNullOrWhiteSpace(disp) ? it.ProductName : disp.Trim();
                        if (string.IsNullOrEmpty(it.ThumbUrl)) it.ThumbUrl = it.ImageUrl;
                        items[it.ProductId] = it;
                        m.Items.Add(it);
                    }

                    var links = DiningDb.Query(_conn, "SELECT Product_ID, Group_ID FROM dbo.Dining_Menu_Item_Option");
                    foreach (DataRow r in links.Rows)
                    {
                        DiningMenuItem it; DiningOptionGroup g;
                        if (items.TryGetValue(DiningDb.Int(r["Product_ID"]), out it) && gmap.TryGetValue(DiningDb.Int(r["Group_ID"]), out g))
                            it.OptionGroups.Add(g);
                    }
                    foreach (var it in m.Items) it.OptionGroups.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
                }
            }
            catch (Exception ex)
            {
                DiningDb.Log(_conn, "โหลดเมนูไม่สำเร็จ: " + ex.Message, "SYSTEM");
            }
            lock (_lock) { _cache = m; _cacheAt = DateTime.UtcNow; }
            return m;
        }

        private static bool IsActiveStatus(object v)
        {
            if (v == null || v == DBNull.Value) return true;
            string s = Convert.ToString(v).Trim();
            if (s.Length == 0) return true;
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // ── แอดมิน: หมวด ─────────────────────────────────────────────────────

        public List<DiningMenuCategory> ListCategories(bool includeInactive)
        {
            var list = new List<DiningMenuCategory>();
            if (!DiningDb.Ready(_conn)) return list;
            var dt = DiningDb.Query(_conn, "SELECT ID, Name_TH, Name_EN, Icon, Sort_Order, Is_Active FROM dbo.Dining_Menu_Category"
                + (includeInactive ? "" : " WHERE Is_Active = 1") + " ORDER BY Sort_Order, Name_TH");
            foreach (DataRow r in dt.Rows)
                list.Add(new DiningMenuCategory
                {
                    Id = DiningDb.Int(r["ID"]), NameTh = DiningDb.Str(r["Name_TH"]), NameEn = DiningDb.Str(r["Name_EN"]),
                    Icon = DiningDb.Str(r["Icon"]), SortOrder = DiningDb.Int(r["Sort_Order"]), IsActive = DiningDb.Bool(r["Is_Active"])
                });
            return list;
        }

        /// <summary>เพิ่ม (Id=0) หรือแก้หมวด — คืน Id</summary>
        public int SaveCategory(DiningMenuCategory c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.NameTh)) throw new ArgumentException("กรุณาระบุชื่อหมวด");
            var p = new Dictionary<string, object>
            {
                { "@n", DiningDb.DbStr(c.NameTh, 100) }, { "@e", DiningDb.DbStr(c.NameEn, 100) }, { "@i", DiningDb.DbStr(c.Icon, 50) },
                { "@s", c.SortOrder }, { "@a", c.IsActive }
            };
            if (c.Id > 0)
            {
                p["@id"] = c.Id;
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Menu_Category SET Name_TH=@n, Name_EN=@e, Icon=@i, Sort_Order=@s, Is_Active=@a WHERE ID=@id", p);
            }
            else
            {
                c.Id = (int)DiningDb.InsertIdentity(_conn, "INSERT INTO dbo.Dining_Menu_Category (Name_TH, Name_EN, Icon, Sort_Order, Is_Active) VALUES (@n,@e,@i,@s,@a)", p);
            }
            Invalidate();
            return c.Id;
        }

        /// <summary>ลบหมวด — รายการในหมวดถูกย้ายเป็น "ไม่มีหมวด"</summary>
        public void DeleteCategory(int id)
        {
            var p = new Dictionary<string, object> { { "@id", id } };
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Menu_Item SET Category_ID = NULL WHERE Category_ID = @id", p);
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Menu_Category WHERE ID = @id", p);
            Invalidate();
        }

        // ── แอดมิน: รายการเมนู ────────────────────────────────────────────────

        /// <summary>สินค้าทั้งหมดใน Product (ใช้งาน) พร้อมสถานะในเมนู QR — สำหรับหน้าเลือกสินค้า (Admin/Dining/Menu)</summary>
        public List<DiningMenuItem> ListProductsForAdmin(bool onlyInMenu)
        {
            var list = new List<DiningMenuItem>();
            if (!DiningDb.Ready(_conn)) return list;
            var menu = new Dictionary<int, DiningMenuItem>();
            foreach (var it in LoadAll().Items) menu[it.ProductId] = it;
            if (onlyInMenu)
            {
                list.AddRange(menu.Values);
                list.Sort((a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
                return list;
            }
            var dt = DiningDb.Query(_conn, @"SELECT p.ID, p.Product_Name, p.Sell_Price, p.Status, p.Barcode, ISNULL(p.Cost_Price,0) AS Cost_Price
                                             FROM Product p WHERE (p.Status = 'True' OR p.Status = '1' OR p.Status IS NULL) ORDER BY p.Product_Name");
            foreach (DataRow r in dt.Rows)
            {
                int pid = DiningDb.Int(r["ID"]);
                DiningMenuItem it;
                if (menu.TryGetValue(pid, out it)) { list.Add(it); continue; }
                list.Add(new DiningMenuItem
                {
                    ProductId = pid, ProductName = DiningDb.Str(r["Product_Name"]) ?? "", Name = DiningDb.Str(r["Product_Name"]) ?? "",
                    Price = DiningDb.Dec(r["Sell_Price"]), CostPrice = DiningDb.Dec(r["Cost_Price"]), Barcode = DiningDb.Str(r["Barcode"]),
                    ProductActive = IsActiveStatus(r["Status"]), IsDining = false
                });
            }
            return list;
        }

        /// <summary>เพิ่ม/แก้รายการเมนู (upsert ตาม ProductId) — ไม่แตะ Product เดิม (ราคา/ชื่อสินค้าแก้ที่หน้าสินค้า)</summary>
        public void SaveMenuItem(DiningMenuItem it, string updatedBy = null)
        {
            if (it == null || it.ProductId <= 0) throw new ArgumentException("ไม่พบสินค้า");
            var p = new Dictionary<string, object>
            {
                { "@pid", it.ProductId }, { "@d", it.IsDining }, { "@cat", DiningDb.Db(it.CategoryId) },
                { "@nth", DiningDb.DbStr(it.Name == it.ProductName ? null : it.Name, 200) }, { "@nen", DiningDb.DbStr(it.NameEn, 200) },
                { "@desc", DiningDb.DbStr(it.Description, 500) }, { "@so", it.IsSoldOut }, { "@sou", DiningDb.Db(it.SoldOutUntil) },
                { "@prep", DiningDb.Db(it.PrepMinutes) }, { "@al", DiningDb.DbStr(it.AllergenTags, 200) }, { "@sort", it.SortOrder },
                { "@z", DiningDb.DbStr(it.AvailableZones, 200) }, { "@ts", it.TrackStock }, { "@rec", it.IsRecommended },
                { "@by", DiningDb.DbStr(updatedBy ?? DiningDb.StaffName(), 100) }
            };
            DiningDb.Exec(_conn, @"
                IF EXISTS (SELECT 1 FROM dbo.Dining_Menu_Item WHERE Product_ID = @pid)
                    UPDATE dbo.Dining_Menu_Item SET Is_Dining=@d, Category_ID=@cat, Display_Name_TH=@nth, Display_Name_EN=@nen, Description_TH=@desc,
                           Is_Sold_Out=@so, Sold_Out_Until=@sou, Prep_Minutes=@prep, Allergen_Tags=@al, Sort_Order=@sort, Available_Zones=@z,
                           Track_Stock=@ts, Is_Recommended=@rec, Updated_At=GETDATE(), Updated_By=@by
                     WHERE Product_ID = @pid
                ELSE
                    INSERT INTO dbo.Dining_Menu_Item (Product_ID, Is_Dining, Category_ID, Display_Name_TH, Display_Name_EN, Description_TH, Is_Sold_Out, Sold_Out_Until,
                           Prep_Minutes, Allergen_Tags, Sort_Order, Available_Zones, Track_Stock, Is_Recommended, Updated_At, Updated_By)
                    VALUES (@pid, @d, @cat, @nth, @nen, @desc, @so, @sou, @prep, @al, @sort, @z, @ts, @rec, GETDATE(), @by)", p);
            Invalidate();
        }

        /// <summary>ปุ่มเร็ว "หมด/มีแล้ว" — until = null คือหมดจนกว่าจะกดคืน</summary>
        public void SetSoldOut(int productId, bool soldOut, DateTime? until = null)
        {
            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Menu_Item SET Is_Sold_Out=@s, Sold_Out_Until=@u, Updated_At=GETDATE() WHERE Product_ID=@p",
                new Dictionary<string, object> { { "@p", productId }, { "@s", soldOut }, { "@u", DiningDb.Db(soldOut ? until : null) } });
            Invalidate();
        }

        /// <summary>เอาออกจากเมนู QR (ลบแถว Dining_Menu_Item + ลิงก์ตัวเลือก; ประวัติออเดอร์คงอยู่ — snapshot แล้ว)</summary>
        public void RemoveMenuItem(int productId)
        {
            var p = new Dictionary<string, object> { { "@p", productId } };
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Menu_Item_Option WHERE Product_ID=@p", p);
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Menu_Item WHERE Product_ID=@p", p);
            Invalidate();
        }

        /// <summary>ผูกกลุ่มตัวเลือกกับรายการ (แทนที่ทั้งชุด)</summary>
        public void SetItemOptionGroups(int productId, IList<int> groupIds)
        {
            var p = new Dictionary<string, object> { { "@p", productId } };
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Menu_Item_Option WHERE Product_ID=@p", p);
            if (groupIds != null)
            {
                var seen = new HashSet<int>();
                foreach (int g in groupIds)
                {
                    if (g <= 0 || !seen.Add(g)) continue;
                    DiningDb.Exec(_conn, "INSERT INTO dbo.Dining_Menu_Item_Option (Product_ID, Group_ID) SELECT @p, @g WHERE EXISTS (SELECT 1 FROM dbo.Dining_Option_Group WHERE ID=@g)",
                        new Dictionary<string, object> { { "@p", productId }, { "@g", g } });
                }
            }
            Invalidate();
        }

        // ── แอดมิน: ตัวเลือก ─────────────────────────────────────────────────

        public List<DiningOptionGroup> ListOptionGroups(bool includeInactive)
        {
            var list = new List<DiningOptionGroup>();
            if (!DiningDb.Ready(_conn)) return list;
            var map = new Dictionary<int, DiningOptionGroup>();
            var dg = DiningDb.Query(_conn, "SELECT ID, Name_TH, Min_Select, Max_Select, Is_Required, Sort_Order, Is_Active FROM dbo.Dining_Option_Group"
                + (includeInactive ? "" : " WHERE Is_Active = 1") + " ORDER BY Sort_Order, Name_TH");
            foreach (DataRow r in dg.Rows)
            {
                var g = new DiningOptionGroup
                {
                    Id = DiningDb.Int(r["ID"]), NameTh = DiningDb.Str(r["Name_TH"]), MinSelect = DiningDb.Int(r["Min_Select"]), MaxSelect = DiningDb.Int(r["Max_Select"]),
                    IsRequired = DiningDb.Bool(r["Is_Required"]), SortOrder = DiningDb.Int(r["Sort_Order"]), IsActive = DiningDb.Bool(r["Is_Active"])
                };
                map[g.Id] = g; list.Add(g);
            }
            var dopt = DiningDb.Query(_conn, "SELECT ID, Group_ID, Name_TH, Price_Delta, Sort_Order, Is_Active FROM dbo.Dining_Option"
                + (includeInactive ? "" : " WHERE Is_Active = 1") + " ORDER BY Sort_Order, ID");
            foreach (DataRow r in dopt.Rows)
            {
                DiningOptionGroup g;
                if (!map.TryGetValue(DiningDb.Int(r["Group_ID"]), out g)) continue;
                g.Options.Add(new DiningOption
                {
                    Id = DiningDb.Int(r["ID"]), GroupId = g.Id, NameTh = DiningDb.Str(r["Name_TH"]), PriceDelta = DiningDb.Dec(r["Price_Delta"]),
                    SortOrder = DiningDb.Int(r["Sort_Order"]), IsActive = DiningDb.Bool(r["Is_Active"])
                });
            }
            return list;
        }

        public int SaveOptionGroup(DiningOptionGroup g)
        {
            if (g == null || string.IsNullOrWhiteSpace(g.NameTh)) throw new ArgumentException("กรุณาระบุชื่อกลุ่มตัวเลือก");
            if (g.MaxSelect > 0 && g.MinSelect > g.MaxSelect) throw new ArgumentException("เลือกขั้นต่ำต้องไม่เกินเลือกสูงสุด");
            var p = new Dictionary<string, object>
            {
                { "@n", DiningDb.DbStr(g.NameTh, 100) }, { "@min", Math.Max(0, g.MinSelect) }, { "@max", Math.Max(0, g.MaxSelect) },
                { "@req", g.IsRequired }, { "@s", g.SortOrder }, { "@a", g.IsActive }
            };
            if (g.Id > 0)
            {
                p["@id"] = g.Id;
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Option_Group SET Name_TH=@n, Min_Select=@min, Max_Select=@max, Is_Required=@req, Sort_Order=@s, Is_Active=@a WHERE ID=@id", p);
            }
            else g.Id = (int)DiningDb.InsertIdentity(_conn, "INSERT INTO dbo.Dining_Option_Group (Name_TH, Min_Select, Max_Select, Is_Required, Sort_Order, Is_Active) VALUES (@n,@min,@max,@req,@s,@a)", p);
            Invalidate();
            return g.Id;
        }

        public void DeleteOptionGroup(int id)
        {
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Option_Group WHERE ID=@id", new Dictionary<string, object> { { "@id", id } });   // cascade → Dining_Option, Dining_Menu_Item_Option
            Invalidate();
        }

        public int SaveOption(DiningOption o)
        {
            if (o == null || o.GroupId <= 0 || string.IsNullOrWhiteSpace(o.NameTh)) throw new ArgumentException("กรุณาระบุชื่อตัวเลือกและกลุ่ม");
            var p = new Dictionary<string, object>
            {
                { "@g", o.GroupId }, { "@n", DiningDb.DbStr(o.NameTh, 100) }, { "@d", DiningPricing.R2(o.PriceDelta) }, { "@s", o.SortOrder }, { "@a", o.IsActive }
            };
            if (o.Id > 0)
            {
                p["@id"] = o.Id;
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Option SET Group_ID=@g, Name_TH=@n, Price_Delta=@d, Sort_Order=@s, Is_Active=@a WHERE ID=@id", p);
            }
            else o.Id = (int)DiningDb.InsertIdentity(_conn, "INSERT INTO dbo.Dining_Option (Group_ID, Name_TH, Price_Delta, Sort_Order, Is_Active) VALUES (@g,@n,@d,@s,@a)", p);
            Invalidate();
            return o.Id;
        }

        public void DeleteOption(int id)
        {
            DiningDb.Exec(_conn, "DELETE FROM dbo.Dining_Option WHERE ID=@id", new Dictionary<string, object> { { "@id", id } });
            Invalidate();
        }
    }
}
