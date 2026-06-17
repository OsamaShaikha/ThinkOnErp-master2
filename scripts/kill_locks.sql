SET SERVEROUTPUT ON
DECLARE
    v_sid      NUMBER;
    v_serial   NUMBER;
    v_object   VARCHAR2(100);
BEGIN
    -- Find the blocking session for object 74349
    SELECT l.session_id, s.serial#, o.object_name
    INTO v_sid, v_serial, v_object
    FROM v$locked_object l
    JOIN dba_objects o ON l.object_id = o.object_id
    JOIN v$session s ON l.session_id = s.sid
    WHERE o.object_id = 74349 AND ROWNUM = 1;

    DBMS_OUTPUT.PUT_LINE('Killing session ' || v_sid || ',' || v_serial || ' blocking ' || v_object);
    EXECUTE IMMEDIATE 'ALTER SYSTEM KILL SESSION ''' || v_sid || ',' || v_serial || ''' IMMEDIATE';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('No blocking session found for object 74349');
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM);
END;
/
EXIT
