-- ================================================================
-- PHASE 16: AI Review Analysis & Report Summary
-- Migration 01: Online review aggregation, sentiment analysis, AI reports
-- ================================================================

-- 1. Online Review Sources (configurable platforms)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Review_Sources')
BEGIN
    CREATE TABLE AI_Review_Sources (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SourceCode NVARCHAR(30) NOT NULL,
        SourceName NVARCHAR(100) NOT NULL,
        IconClass NVARCHAR(50),
        BrandColor NVARCHAR(20),
        IsEnabled BIT DEFAULT 0,
        ApiConfig NVARCHAR(MAX),
        LastFetchDate DATETIME,
        TotalReviews INT DEFAULT 0,
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_ReviewSource UNIQUE (SourceCode)
    );

    INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES
    ('INTERNAL',    N'รีวิวจากระบบ',         'fas fa-home',          '#5D4037', 1),
    ('GOOGLE',      N'Google Reviews',        'fab fa-google',        '#4285F4', 0),
    ('FACEBOOK',    N'Facebook Reviews',      'fab fa-facebook',      '#1877F2', 0),
    ('AGODA',       N'Agoda',                 'fas fa-bed',           '#5542F6', 0),
    ('BOOKING',     N'Booking.com',           'fas fa-suitcase',      '#003580', 0),
    ('TRIPADVISOR', N'TripAdvisor',           'fab fa-tripadvisor',   '#34E0A1', 0),
    ('EXPEDIA',     N'Expedia',               'fas fa-globe',         '#FFCC00', 0),
    ('TRAVELOKA',   N'Traveloka',             'fas fa-plane',         '#0194F3', 0),
    ('PANTIP',      N'Pantip',                'fas fa-comments',      '#7A2D8F', 0),
    ('TIKTOK',      N'TikTok',                'fab fa-tiktok',        '#000000', 0),
    ('LEMON8',      N'Lemon8',                'fas fa-lemon',         '#FFE135', 0),
    ('WONGNAI',     N'Wongnai',               'fas fa-utensils',      '#ED1C24', 0),
    ('TWITTER',     N'X (Twitter)',            'fab fa-x-twitter',     '#000000', 0),
    ('INSTAGRAM',   N'Instagram',             'fab fa-instagram',     '#E4405F', 0),
    ('YOUTUBE',     N'YouTube',               'fab fa-youtube',       '#FF0000', 0);

    PRINT 'Created table: AI_Review_Sources with seed data';
END
GO

-- 1b. Add social platform sources if table exists but missing new sources
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Review_Sources')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'PANTIP')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('PANTIP', N'Pantip', 'fas fa-comments', '#7A2D8F', 0);
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'TIKTOK')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('TIKTOK', N'TikTok', 'fab fa-tiktok', '#000000', 0);
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'LEMON8')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('LEMON8', N'Lemon8', 'fas fa-lemon', '#FFE135', 0);
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'WONGNAI')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('WONGNAI', N'Wongnai', 'fas fa-utensils', '#ED1C24', 0);
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'TWITTER')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('TWITTER', N'X (Twitter)', 'fab fa-x-twitter', '#000000', 0);
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'INSTAGRAM')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('INSTAGRAM', N'Instagram', 'fab fa-instagram', '#E4405F', 0);
    IF NOT EXISTS (SELECT 1 FROM AI_Review_Sources WHERE SourceCode = 'YOUTUBE')
        INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES ('YOUTUBE', N'YouTube', 'fab fa-youtube', '#FF0000', 0);
    PRINT 'Ensured social platform sources exist';
END
GO

-- 2. Aggregated Online Reviews
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Online_Reviews')
BEGIN
    CREATE TABLE AI_Online_Reviews (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        SourceCode NVARCHAR(30) NOT NULL,
        PlatformReviewId NVARCHAR(200),
        ReviewerName NVARCHAR(200),
        ReviewerAvatar NVARCHAR(500),
        Rating FLOAT,
        ReviewTitle NVARCHAR(300),
        ReviewText NVARCHAR(MAX),
        ReviewDate DATETIME,
        Language NVARCHAR(10) DEFAULT 'TH',
        -- AI Analysis
        Sentiment NVARCHAR(20),
        SentimentScore FLOAT,
        Topics NVARCHAR(500),
        TopicScores NVARCHAR(MAX),
        AISummary NVARCHAR(500),
        SuggestedResponse NVARCHAR(MAX),
        ResponseStatus NVARCHAR(20) DEFAULT 'PENDING',
        ActualResponse NVARCHAR(MAX),
        RespondedDate DATETIME,
        RespondedBy NVARCHAR(100),
        -- Meta
        IsAnalyzed BIT DEFAULT 0,
        IsFlagged BIT DEFAULT 0,
        FlagReason NVARCHAR(200),
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AIReview_Source (SourceCode),
        INDEX IX_AIReview_Sentiment (Sentiment),
        INDEX IX_AIReview_Rating (Rating),
        INDEX IX_AIReview_Date (ReviewDate DESC),
        INDEX IX_AIReview_PlatformId (SourceCode, PlatformReviewId)
    );

    PRINT 'Created table: AI_Online_Reviews';
END
GO

-- 3. Add sentiment columns to existing Guest_Reviews
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Reviews')
BEGIN
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Guest_Reviews' AND COLUMN_NAME = 'Sentiment')
    BEGIN
        ALTER TABLE Guest_Reviews ADD Sentiment NVARCHAR(20) NULL;
        ALTER TABLE Guest_Reviews ADD SentimentScore FLOAT NULL;
        ALTER TABLE Guest_Reviews ADD AITopics NVARCHAR(500) NULL;
        ALTER TABLE Guest_Reviews ADD AISummary NVARCHAR(500) NULL;
        ALTER TABLE Guest_Reviews ADD AISuggestedResponse NVARCHAR(MAX) NULL;
        ALTER TABLE Guest_Reviews ADD AITopicScores NVARCHAR(MAX) NULL;
        ALTER TABLE Guest_Reviews ADD IsAIAnalyzed BIT DEFAULT 0;
        ALTER TABLE Guest_Reviews ADD IsFlagged BIT DEFAULT 0;
        ALTER TABLE Guest_Reviews ADD FlagReason NVARCHAR(200) NULL;
        PRINT 'Added AI sentiment columns to Guest_Reviews';
    END
END
GO

-- 4. AI Report Summaries
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Report_Summaries')
BEGIN
    CREATE TABLE AI_Report_Summaries (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        ReportType NVARCHAR(30) NOT NULL,
        PeriodType NVARCHAR(20) NOT NULL,
        PeriodStart DATE NOT NULL,
        PeriodEnd DATE NOT NULL,
        DataSnapshot NVARCHAR(MAX),
        AISummary NVARCHAR(MAX) NOT NULL,
        KeyInsights NVARCHAR(MAX),
        Recommendations NVARCHAR(MAX),
        TokensUsed INT DEFAULT 0,
        GeneratedBy NVARCHAR(100),
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AIReport_Type (ReportType, PeriodType),
        INDEX IX_AIReport_Period (PeriodStart, PeriodEnd)
    );

    PRINT 'Created table: AI_Report_Summaries';
END
GO

-- 5. Review Analytics Aggregation View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_AI_Review_AllSources')
    DROP VIEW vw_AI_Review_AllSources;
GO

CREATE VIEW vw_AI_Review_AllSources AS
    SELECT
        'INTERNAL' AS SourceCode,
        gr.ID,
        c.Name AS ReviewerName,
        NULL AS ReviewerAvatar,
        CAST(gr.OverallRating AS FLOAT) AS Rating,
        gr.ReviewTitle,
        ISNULL(gr.ReviewText, '') + CASE WHEN gr.Pros IS NOT NULL THEN N' ข้อดี: ' + gr.Pros ELSE '' END + CASE WHEN gr.Cons IS NOT NULL THEN N' ข้อเสีย: ' + gr.Cons ELSE '' END AS ReviewText,
        gr.SubmittedDate AS ReviewDate,
        gr.Sentiment,
        gr.SentimentScore,
        gr.AITopics AS Topics,
        gr.AISummary,
        gr.AISuggestedResponse AS SuggestedResponse,
        gr.Status AS ResponseStatus,
        gr.ResponseText AS ActualResponse,
        CASE WHEN gr.IsAIAnalyzed = 1 THEN 1 ELSE 0 END AS IsAnalyzed,
        ISNULL(gr.IsFlagged, 0) AS IsFlagged,
        gr.FlagReason
    FROM Guest_Reviews gr
    LEFT JOIN Customer c ON c.MobilePhone = gr.Customer_MobilePhone
    WHERE gr.Status IN ('APPROVED', 'PENDING')

    UNION ALL

    SELECT
        aor.SourceCode,
        aor.ID,
        aor.ReviewerName,
        aor.ReviewerAvatar,
        aor.Rating,
        aor.ReviewTitle,
        aor.ReviewText,
        aor.ReviewDate,
        aor.Sentiment,
        aor.SentimentScore,
        aor.Topics,
        aor.AISummary,
        aor.SuggestedResponse,
        aor.ResponseStatus,
        aor.ActualResponse,
        CAST(aor.IsAnalyzed AS INT) AS IsAnalyzed,
        ISNULL(aor.IsFlagged, 0) AS IsFlagged,
        aor.FlagReason
    FROM AI_Online_Reviews aor;
GO

PRINT 'Created view: vw_AI_Review_AllSources';
GO
