-- Seed reference data for FC Engine
-- Run this after migrations have been applied

-- Insert sample institutions
INSERT INTO institutions (institution_code, institution_name, license_type, is_active)
VALUES
    ('FC001', 'Sample Finance Company Ltd', 'Finance Company', 1),
    ('FC002', 'Example Microfinance Bank', 'Microfinance Bank', 1);

-- Insert return periods for 2024-2025
DECLARE @year INT = 2024;
DECLARE @month INT = 1;

WHILE @year <= 2025
BEGIN
    WHILE @month <= 12
    BEGIN
        -- Monthly periods
        IF NOT EXISTS (SELECT 1 FROM return_periods WHERE year = @year AND month = @month AND frequency = 'Monthly')
        BEGIN
            INSERT INTO return_periods (year, month, frequency, reporting_date, is_open)
            VALUES (@year, @month, 'Monthly', EOMONTH(DATEFROMPARTS(@year, @month, 1)), 1);
        END

        -- Quarterly periods (March, June, September, December)
        IF @month IN (3, 6, 9, 12) AND NOT EXISTS (SELECT 1 FROM return_periods WHERE year = @year AND month = @month AND frequency = 'Quarterly')
        BEGIN
            INSERT INTO return_periods (year, month, frequency, reporting_date, is_open)
            VALUES (@year, @month, 'Quarterly', EOMONTH(DATEFROMPARTS(@year, @month, 1)), 1);
        END

        -- Semi-Annual periods (June, December)
        IF @month IN (6, 12) AND NOT EXISTS (SELECT 1 FROM return_periods WHERE year = @year AND month = @month AND frequency = 'SemiAnnual')
        BEGIN
            INSERT INTO return_periods (year, month, frequency, reporting_date, is_open)
            VALUES (@year, @month, 'SemiAnnual', EOMONTH(DATEFROMPARTS(@year, @month, 1)), 1);
        END

        SET @month = @month + 1;
    END
    SET @month = 1;
    SET @year = @year + 1;
END

PRINT 'Reference data seeded successfully.';
