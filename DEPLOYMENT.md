# TakeTime Deployment Guide

## Overview
คู่มือการ deploy TakeTime HR Management System ไปยัง IIS Production Server

---

## ขั้นตอนที่ 1: เตรียม Production Server

### 1.1 ติดตั้ง Prerequisites บน Server

```powershell
# ติดตั้ง IIS และ Features ที่จำเป็น
Install-WindowsFeature -Name Web-Server -IncludeManagementTools
Install-WindowsFeature -Name Web-Asp-Net45
Install-WindowsFeature -Name Web-Net-Ext45
Install-WindowsFeature -Name Web-ISAPI-Ext
Install-WindowsFeature -Name Web-ISAPI-Filter
Install-WindowsFeature -Name Web-Mgmt-Console
Install-WindowsFeature -Name Web-Mgmt-Service

# ติดตั้ง .NET Framework 4.7.2 (ถ้ายังไม่มี)
# ดาวน์โหลดจาก: https://dotnet.microsoft.com/download/dotnet-framework/net472
```

### 1.2 สร้าง Application Pool

```powershell
Import-Module WebAdministration

# สร้าง App Pool สำหรับ TakeTime
New-WebAppPool -Name "TakeTimeAppPool"

# ตั้งค่า App Pool
Set-ItemProperty IIS:\AppPools\TakeTimeAppPool -Name "managedRuntimeVersion" -Value "v4.0"
Set-ItemProperty IIS:\AppPools\TakeTimeAppPool -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty IIS:\AppPools\TakeTimeAppPool -Name "processModel.identityType" -Value "ApplicationPoolIdentity"

# ตั้งค่า Idle Timeout (ป้องกัน cold start)
Set-ItemProperty IIS:\AppPools\TakeTimeAppPool -Name "processModel.idleTimeout" -Value "00:00:00"
```

### 1.3 สร้าง IIS Website

```powershell
# สร้าง folder สำหรับเว็บไซต์
$sitePath = "C:\inetpub\wwwroot\TakeTime"
New-Item -ItemType Directory -Path $sitePath -Force

# สร้างเว็บไซต์
New-Website -Name "TakeTime" `
    -PhysicalPath $sitePath `
    -ApplicationPool "TakeTimeAppPool" `
    -Port 80 `
    -HostHeader "taketime.yourdomain.com"

# ถ้าต้องการ HTTPS
New-WebBinding -Name "TakeTime" -Protocol "https" -Port 443 -HostHeader "taketime.yourdomain.com"
```

### 1.4 ตั้งค่า Folder Permissions

```powershell
$sitePath = "C:\inetpub\wwwroot\TakeTime"

# ให้สิทธิ์ IIS_IUSRS
$acl = Get-Acl $sitePath
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS_IUSRS", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl $sitePath $acl

# ให้สิทธิ์ Write สำหรับ Documents folder
$documentsPath = "$sitePath\Documents"
New-Item -ItemType Directory -Path $documentsPath -Force

$acl = Get-Acl $documentsPath
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS_IUSRS", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl $documentsPath $acl
```

---

## ขั้นตอนที่ 2: ติดตั้ง GitHub Self-Hosted Runner

### 2.1 ดาวน์โหลด Runner บน Production Server

```powershell
# สร้าง folder สำหรับ runner
mkdir C:\actions-runner
cd C:\actions-runner

# ดาวน์โหลด runner (เลือก version ล่าสุด)
Invoke-WebRequest -Uri https://github.com/actions/runner/releases/download/v2.311.0/actions-runner-win-x64-2.311.0.zip -OutFile actions-runner-win-x64.zip

# แตกไฟล์
Expand-Archive -Path actions-runner-win-x64.zip -DestinationPath .
```

### 2.2 Configure Runner

```powershell
# ไปที่ GitHub Repository > Settings > Actions > Runners > New self-hosted runner
# คัดลอก token มาใช้

.\config.cmd --url https://github.com/YOUR_ORG/TakeTime --token YOUR_TOKEN

# ตั้งค่าให้รันเป็น Windows Service
.\svc.cmd install
.\svc.cmd start
```

---

## ขั้นตอนที่ 3: ตั้งค่า GitHub Repository

### 3.1 สร้าง Repository Variables

ไปที่ **Settings > Secrets and variables > Actions > Variables** แล้วเพิ่ม:

| Variable Name | Value | Description |
|--------------|-------|-------------|
| `IIS_APP_POOL_NAME` | `TakeTimeAppPool` | ชื่อ Application Pool |
| `IIS_SITE_PATH` | `C:\inetpub\wwwroot\TakeTime` | Path ของเว็บไซต์ |
| `BACKUP_PATH` | `C:\Backups\TakeTime` | Path สำหรับ backup |
| `SITE_URL` | `https://taketime.yourdomain.com` | URL สำหรับ health check |

### 3.2 สร้าง Repository Secrets

ไปที่ **Settings > Secrets and variables > Actions > Secrets** แล้วเพิ่ม:

| Secret Name | Description |
|------------|-------------|
| `PROD_CONNECTION_STRING` | Connection string สำหรับ production database |

---

## ขั้นตอนที่ 4: Deploy แบบ Manual (ครั้งแรก)

### 4.1 Build บน Development Machine

```powershell
# ไปที่ folder โปรเจค
cd "C:\path\to\TakeTime"

# Restore packages
nuget restore "Take Time BangPhra\Take Time BangPhra.csproj"

# Build
msbuild "Take Time BangPhra\Take Time BangPhra.csproj" `
    /p:Configuration=Release `
    /p:DeployOnBuild=true `
    /p:WebPublishMethod=FileSystem `
    /p:publishUrl="C:\publish\TakeTime"
```

### 4.2 Copy ไปยัง Production Server

```powershell
# บน Development Machine
$sourcePath = "C:\publish\TakeTime\*"
$destPath = "\\PRODUCTION_SERVER\c$\inetpub\wwwroot\TakeTime"

# Stop App Pool ก่อน
Invoke-Command -ComputerName PRODUCTION_SERVER -ScriptBlock {
    Import-Module WebAdministration
    Stop-WebAppPool -Name "TakeTimeAppPool"
}

# Copy files
Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force

# Start App Pool
Invoke-Command -ComputerName PRODUCTION_SERVER -ScriptBlock {
    Import-Module WebAdministration
    Start-WebAppPool -Name "TakeTimeAppPool"
}
```

### 4.3 ตั้งค่า Web.config สำหรับ Production

```xml
<!-- แก้ไข Connection String -->
<connectionStrings>
    <add name="TaketimeConnectionString"
         connectionString="Server=PROD_SERVER;Database=Taketime;User Id=xxx;Password=xxx;"
         providerName="System.Data.SqlClient" />
</connectionStrings>

<!-- ปิด Debug Mode -->
<compilation debug="false" targetFramework="4.7.2" />

<!-- ตั้งค่า Custom Errors -->
<customErrors mode="RemoteOnly" defaultRedirect="~/Error.aspx">
    <error statusCode="404" redirect="~/404.aspx"/>
</customErrors>
```

---

## ขั้นตอนที่ 5: Deploy อัตโนมัติ (GitHub Actions)

### 5.1 Trigger Deployment

1. **Push to main/master branch** - Deploy อัตโนมัติ
2. **Manual trigger** - ไปที่ Actions > Deploy to Production IIS > Run workflow

### 5.2 Monitor Deployment

1. ไปที่ **Actions** tab ใน GitHub
2. คลิกที่ workflow run ที่ต้องการดู
3. ดู logs ของแต่ละ step

---

## ขั้นตอนที่ 6: Verify Deployment

### 6.1 ตรวจสอบ Website

```powershell
# ตรวจสอบ IIS
Import-Module WebAdministration

# ดูสถานะ App Pool
Get-WebAppPoolState -Name "TakeTimeAppPool"

# ดูสถานะ Website
Get-Website -Name "TakeTime"

# ทดสอบเรียก URL
Invoke-WebRequest -Uri "https://taketime.yourdomain.com" -UseBasicParsing
```

### 6.2 ตรวจสอบ Logs

```powershell
# ดู IIS Logs
Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\*.log" -Tail 50

# ดู Event Log
Get-EventLog -LogName Application -Source "ASP.NET*" -Newest 20
```

---

## Troubleshooting

### ปัญหา: 500 Internal Server Error

```powershell
# เปิด detailed errors
# แก้ไข Web.config
<system.webServer>
    <httpErrors errorMode="Detailed" />
</system.webServer>
```

### ปัญหา: Permission Denied

```powershell
# ตรวจสอบสิทธิ์ App Pool Identity
$sitePath = "C:\inetpub\wwwroot\TakeTime"
icacls $sitePath /grant "IIS AppPool\TakeTimeAppPool:(OI)(CI)M"
```

### ปัญหา: Database Connection Failed

1. ตรวจสอบ Connection String ใน Web.config
2. ตรวจสอบ Firewall rules
3. ตรวจสอบ SQL Server authentication

### ปัญหา: PDF ไม่ Generate

```powershell
# ให้สิทธิ์ Write ที่ Documents folder
$documentsPath = "C:\inetpub\wwwroot\TakeTime\Documents"
icacls $documentsPath /grant "IIS AppPool\TakeTimeAppPool:(OI)(CI)M"
```

---

## Rollback

### Manual Rollback

```powershell
$backupPath = "C:\Backups\TakeTime"
$sitePath = "C:\inetpub\wwwroot\TakeTime"

# หา backup ล่าสุด
$latestBackup = Get-ChildItem -Path $backupPath -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

# Stop App Pool
Import-Module WebAdministration
Stop-WebAppPool -Name "TakeTimeAppPool"

# Restore from backup
Copy-Item -Path "$($latestBackup.FullName)\*" -Destination $sitePath -Recurse -Force

# Start App Pool
Start-WebAppPool -Name "TakeTimeAppPool"
```

---

## Security Checklist

- [ ] ปิด Debug mode ใน Web.config
- [ ] ใช้ HTTPS
- [ ] ตั้งค่า Custom Error pages
- [ ] ซ่อน Server headers
- [ ] ตั้งค่า Security headers (X-Frame-Options, X-XSS-Protection, etc.)
- [ ] จำกัด IP ที่เข้าถึง Admin pages (ถ้าจำเป็น)
- [ ] Enable audit logging
- [ ] Regular backup schedule

---

## Contact

หากพบปัญหาในการ deploy กรุณาติดต่อ:
- Technical Support: [your-email@domain.com]
- GitHub Issues: [repository-issues-url]
