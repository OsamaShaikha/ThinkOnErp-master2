-- Add refresh token columns to SYS_USERS table
-- This script adds support for refresh token functionality

-- Add refresh token column to store the token
ALTER TABLE SYS_USERS ADD (
    REFRESH_TOKEN VARCHAR2(500),
    REFRESH_TOKEN_EXPIRY DATE
);

-- Add comment to columns
COMMENT ON COLUMN SYS_USERS.REFRESH_TOKEN IS 'Refresh token for JWT authentication';
COMMENT ON COLUMN SYS_USERS.REFRESH_TOKEN_EXPIRY IS 'Expiration date for the refresh token';
