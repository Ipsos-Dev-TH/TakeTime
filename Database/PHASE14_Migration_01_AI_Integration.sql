-- ================================================================
-- PHASE 14: AI Integration (DeepSeek)
-- Migration 01: AI Configuration & Chat History
-- ================================================================

-- 1. AI Configuration Table (key-value like Accounting_Integration_Config)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Integration_Config')
BEGIN
    CREATE TABLE AI_Integration_Config (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        ConfigKey NVARCHAR(100) NOT NULL,
        ConfigValue NVARCHAR(MAX) NOT NULL,
        Description NVARCHAR(255),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_AI_Integration_Config_Key UNIQUE (ConfigKey)
    );

    INSERT INTO AI_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('AI_Provider',            'deepseek',                           'AI Provider (deepseek)'),
    ('AI_BaseUrl',             'https://api.deepseek.com',           'DeepSeek API base URL'),
    ('AI_ApiKey_Encrypted',    '',                                   'Encrypted API Key for DeepSeek'),
    ('AI_Model',               'deepseek-chat',                      'Model name (deepseek-chat, deepseek-reasoner)'),
    ('AI_Enabled',             'false',                              'Enable/disable AI features (true/false)'),
    ('AI_MaxTokens',           '2048',                               'Maximum tokens per response'),
    ('AI_Temperature',         '0.7',                                'Temperature (0.0 - 2.0)'),
    ('AI_TimeoutSec',          '30',                                 'HTTP request timeout in seconds'),
    ('AI_SystemPrompt',        N'คุณเป็นผู้ช่วย AI ของ TakeTime BangPhra ที่พักริมทะเล ตอบคำถามเป็นภาษาไทย สุภาพ เป็นมิตร ให้ข้อมูลเกี่ยวกับที่พัก สิ่งอำนวยความสะดวก กิจกรรม สถานที่ใกล้เคียง และบริการต่างๆ หากไม่ทราบข้อมูลให้แนะนำติดต่อ Front Desk',
                                                                     'System prompt for AI assistant'),
    ('AI_GuestChat_Enabled',   'false',                              'Enable AI assistant in guest chat'),
    ('AI_AdminSuggest_Enabled','false',                              'Enable AI reply suggestions for admin chat'),
    ('AI_MaxHistory',          '20',                                 'Max conversation history messages to include');

    PRINT 'Created table: AI_Integration_Config';
END
GO

-- 2. AI Chat History (stores AI conversations separate from Guest_Chat)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Chat_History')
BEGIN
    CREATE TABLE AI_Chat_History (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        SessionKey NVARCHAR(100) NOT NULL,
        Role NVARCHAR(20) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        TokensUsed INT NULL,
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AI_Chat_SessionKey (SessionKey),
        INDEX IX_AI_Chat_Date (Created_Date)
    );

    PRINT 'Created table: AI_Chat_History';
END
GO

-- 3. AI Usage Log (track API calls and costs)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Usage_Log')
BEGIN
    CREATE TABLE AI_Usage_Log (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        RequestDate DATETIME DEFAULT GETDATE(),
        SessionKey NVARCHAR(100),
        Model NVARCHAR(50),
        PromptTokens INT,
        CompletionTokens INT,
        TotalTokens INT,
        ResponseTimeMs INT,
        Success BIT DEFAULT 1,
        ErrorMessage NVARCHAR(500) NULL,
        INDEX IX_AI_Usage_Date (RequestDate)
    );

    PRINT 'Created table: AI_Usage_Log';
END
GO
