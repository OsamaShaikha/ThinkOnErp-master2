-- Check the current signature of SP_SYS_COMPANY_UPDATE procedure
SELECT 
    argument_name,
    position,
    data_type,
    in_out,
    data_length
FROM user_arguments
WHERE object_name = 'SP_SYS_COMPANY_UPDATE'
ORDER BY position;
