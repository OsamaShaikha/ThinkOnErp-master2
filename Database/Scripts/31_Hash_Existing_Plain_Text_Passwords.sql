-- =============================================
-- ThinkOnErp API - Hash Existing Plain Text Passwords
-- Description: Updates any existing plain text passwords to SHA-256 hashed passwords
-- Note: This script identifies plain text passwords by their length (SHA-256 hashes are 64 characters)
-- =============================================

-- =============================================
-- Check for users with plain text passwords (length != 64)
-- =============================================
SELECT 
    ROW_ID,
    USER_NAME,
    LENGTH(PASSWORD) as PASSWORD_LENGTH,
    CASE 
        WHEN LENGTH(PASSWORD) = 64 THEN 'Already Hashed'
        ELSE 'Plain Text - Needs Hashing'
    END as PASSWORD_STATUS
FROM SYS_USERS
WHERE IS_ACTIVE = '1'
ORDER BY PASSWORD_STATUS, USER_NAME;

-- =============================================
-- Display count of users needing password hashing
-- =============================================
SELECT 
    COUNT(*) as TOTAL_USERS,
    SUM(CASE WHEN LENGTH(PASSWORD) = 64 THEN 1 ELSE 0 END) as ALREADY_HASHED,
    SUM(CASE WHEN LENGTH(PASSWORD) != 64 THEN 1 ELSE 0 END) as NEEDS_HASHING
FROM SYS_USERS
WHERE IS_ACTIVE = '1';

-- =============================================
-- WARNING: Manual Password Update Required
-- =============================================
-- Due to security requirements, passwords must be hashed using SHA-256 in the API layer.
-- This script cannot automatically hash passwords because:
-- 1. Oracle SQL doesn't have built-in SHA-256 function in all versions
-- 2. Password hashing should be done consistently through the API layer
-- 3. We need to maintain the same hashing algorithm and format

-- =============================================
-- SOLUTION: Use API to Update Passwords
-- =============================================
-- For any users with plain text passwords (LENGTH != 64):
-- 1. Use the Reset Password API endpoint: POST /api/users/{id}/reset-password
-- 2. This will generate a secure temporary password and hash it properly
-- 3. Provide the temporary password to the user
-- 4. User should change password using: PUT /api/users/{id}/change-password

-- =============================================
-- Alternative: Manual Update (if needed)
-- =============================================
-- If you need to manually set a specific password for testing:
-- 1. Hash the password using the API's PasswordHashingService (SHA-256)
-- 2. Update the database with the hashed value
-- Example for password "Admin@123":
-- UPDATE SYS_USERS 
-- SET PASSWORD = 'F2CA1BB6C7E907D06DAFE4687E579FDE76B37F4FF8F5F84F48E3DFA22F4F4637'
-- WHERE USER_NAME = 'testuser';

-- =============================================
-- Verification Query
-- =============================================
-- Run this after updating passwords to verify all are hashed:
-- SELECT 
--     USER_NAME,
--     LENGTH(PASSWORD) as PASSWORD_LENGTH,
--     CASE 
--         WHEN LENGTH(PASSWORD) = 64 THEN 'Properly Hashed'
--         ELSE 'Still Plain Text'
--     END as STATUS
-- FROM SYS_USERS
-- WHERE IS_ACTIVE = '1'
-- ORDER BY STATUS, USER_NAME;
