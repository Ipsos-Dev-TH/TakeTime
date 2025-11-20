using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Product Data Access - Handles product images and information
    /// </summary>
    public class ProductDataAccess
    {
        private readonly code _code;
        private readonly string _connectionString;

        public ProductDataAccess(string connectionString)
        {
            _code = new code();
            _connectionString = connectionString;
        }

        #region Image Queries

        /// <summary>
        /// Get all images for a product
        /// </summary>
        public DataTable GetProductImages(string productType, int productId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productType", productType },
                { "@productId", productId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT * FROM Product_Images
                  WHERE ProductType = @productType
                  AND Product_ID = @productId
                  AND Status = 1
                  ORDER BY ImageOrder",
                parameters);
        }

        /// <summary>
        /// Get main image for a product
        /// </summary>
        public DataTable GetMainImage(string productType, int productId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productType", productType },
                { "@productId", productId }
            };

            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP 1 * FROM Product_Images
                  WHERE ProductType = @productType
                  AND Product_ID = @productId
                  AND IsMainImage = 1
                  AND Status = 1",
                parameters);
        }

        /// <summary>
        /// Insert product image
        /// </summary>
        public long InsertProductImage(
            string productType,
            int productId,
            string imageUrl,
            string thumbnailUrl,
            int imageOrder,
            bool isMainImage,
            string caption,
            int? uploadedBy)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productType", productType },
                { "@productId", productId },
                { "@imageUrl", imageUrl },
                { "@thumbnailUrl", thumbnailUrl },
                { "@imageOrder", imageOrder },
                { "@isMainImage", isMainImage },
                { "@caption", caption },
                { "@uploadedBy", uploadedBy }
            };

            return _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO Product_Images (
                    ProductType, Product_ID, ImageURL, ThumbnailURL,
                    ImageOrder, IsMainImage, Caption, UploadedBy_AdminID, Status
                  )
                  VALUES (
                    @productType, @productId, @imageUrl, @thumbnailUrl,
                    @imageOrder, @isMainImage, @caption, @uploadedBy, 1
                  );
                  SELECT SCOPE_IDENTITY();",
                parameters);
        }

        /// <summary>
        /// Delete image (soft delete)
        /// </summary>
        public void DeleteProductImage(long imageId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@imageId", imageId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                "UPDATE Product_Images SET Status = 0 WHERE ID = @imageId",
                parameters);
        }

        #endregion

        #region Product Queries

        /// <summary>
        /// Get accommodations with main images
        /// </summary>
        public DataTable GetAccommodationsWithImages()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_AccommodationWithImages ORDER BY AccomName",
                null);
        }

        /// <summary>
        /// Get items with main images
        /// </summary>
        public DataTable GetItemsWithImages()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_ItemsWithImages ORDER BY ItemName",
                null);
        }

        #endregion
    }
}
