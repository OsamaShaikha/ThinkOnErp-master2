-- ============================================================================
-- Script: 125_Create_SYS_SYSTEM_SCREEN_FEATURE_Hybrid.sql
-- Description: Creates the Hybrid Many-to-Many relationship table
--              SYS_SYSTEM_SCREEN_FEATURE for assigning features to:
--              1. A screen within a specific system (SCREEN_ID IS NOT NULL)
--              2. A system globally (SCREEN_ID IS NULL)
-- ============================================================================

-- 1. Create Sequence for SYS_SYSTEM_SCREEN_FEATURE
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_SEQUENCES WHERE SEQUENCE_NAME = 'SEQ_SYS_SYS_SCREEN_FEATURE';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE SEQ_SYS_SYS_SCREEN_FEATURE START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE';
    END IF;
END;
/

-- 2. Create Table SYS_SYSTEM_SCREEN_FEATURE
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_TABLES WHERE TABLE_NAME = 'SYS_SYSTEM_SCREEN_FEATURE';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE SYS_SYSTEM_SCREEN_FEATURE (
                "Id" NUMBER(19) PRIMARY KEY,
                SYSTEM_ID NUMBER(19) NOT NULL,
                SCREEN_ID NUMBER(19) NULL,
                FEATURE_ID NUMBER(19) NOT NULL,
                IS_ACTIVE NUMBER(1) DEFAULT 1 CHECK (IS_ACTIVE IN (0, 1)),
                CREATION_USER NVARCHAR2(100) NOT NULL,
                CREATION_DATE DATE DEFAULT SYSDATE,
                UPDATE_USER NVARCHAR2(100),
                UPDATE_DATE DATE,
                CONSTRAINT FK_SYS_SCR_FEAT_SYS FOREIGN KEY (SYSTEM_ID) REFERENCES SYS_SYSTEM("Id") ON DELETE CASCADE,
                CONSTRAINT FK_SYS_SCR_FEAT_SCR FOREIGN KEY (SCREEN_ID) REFERENCES SYS_SCREEN("Id") ON DELETE CASCADE,
                CONSTRAINT FK_SYS_SCR_FEAT_FEAT FOREIGN KEY (FEATURE_ID) REFERENCES SYS_FEATURE("Id") ON DELETE CASCADE
            )';
    END IF;
END;
/

-- 3. Create Supporting Indexes (Handling NULL SCREEN_ID with NVL for composite uniqueness)
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IX_SYS_SYS_SCR_FEAT_UK';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX IX_SYS_SYS_SCR_FEAT_UK ON SYS_SYSTEM_SCREEN_FEATURE(SYSTEM_ID, NVL(SCREEN_ID, 0), FEATURE_ID)';
    END IF;

    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IDX_SYS_SYS_SCR_FEAT_SCR';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_SYS_SYS_SCR_FEAT_SCR ON SYS_SYSTEM_SCREEN_FEATURE(SCREEN_ID)';
    END IF;

    SELECT COUNT(*) INTO v_count FROM USER_INDEXES WHERE INDEX_NAME = 'IDX_SYS_SYS_SCR_FEAT_FEAT';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_SYS_SYS_SCR_FEAT_FEAT ON SYS_SYSTEM_SCREEN_FEATURE(FEATURE_ID)';
    END IF;
END;
/

-- 4. Migrate Existing SYS_SCREEN_FEATURE Data
-- For each existing (SCREEN_ID, FEATURE_ID) link:
-- Associate it with all systems currently mapped to that screen in SYS_SYSTEM_SCREEN (and legacy SYS_SCREEN.SYSTEM_ID).
DECLARE
    v_migrated NUMBER := 0;
BEGIN
    FOR r IN (
        SELECT DISTINCT 
               ss.SYSTEM_ID,
               sf.SCREEN_ID,
               sf.FEATURE_ID
        FROM SYS_SCREEN_FEATURE sf
        INNER JOIN SYS_SYSTEM_SCREEN ss ON ss.SCREEN_ID = sf.SCREEN_ID
        WHERE ss.IS_ACTIVE = 1
          AND NOT EXISTS (
              SELECT 1 FROM SYS_SYSTEM_SCREEN_FEATURE existing
              WHERE existing.SYSTEM_ID = ss.SYSTEM_ID
                AND existing.SCREEN_ID = sf.SCREEN_ID
                AND existing.FEATURE_ID = sf.FEATURE_ID
          )
        UNION
        SELECT DISTINCT
               s.SYSTEM_ID,
               sf.SCREEN_ID,
               sf.FEATURE_ID
        FROM SYS_SCREEN_FEATURE sf
        INNER JOIN SYS_SCREEN s ON s."Id" = sf.SCREEN_ID
        WHERE s.SYSTEM_ID IS NOT NULL
          AND s.IS_ACTIVE = 1
          AND NOT EXISTS (
              SELECT 1 FROM SYS_SYSTEM_SCREEN_FEATURE existing
              WHERE existing.SYSTEM_ID = s.SYSTEM_ID
                AND existing.SCREEN_ID = sf.SCREEN_ID
                AND existing.FEATURE_ID = sf.FEATURE_ID
          )
    ) LOOP
        INSERT INTO SYS_SYSTEM_SCREEN_FEATURE (
            "Id", SYSTEM_ID, SCREEN_ID, FEATURE_ID, IS_ACTIVE, CREATION_USER, CREATION_DATE
        ) VALUES (
            SEQ_SYS_SYS_SCREEN_FEATURE.NEXTVAL,
            r.SYSTEM_ID,
            r.SCREEN_ID,
            r.FEATURE_ID,
            1,
            'MIGRATION_HYBRID',
            SYSDATE
        );
        v_migrated := v_migrated + 1;
    END LOOP;

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Migrated ' || v_migrated || ' system-screen-feature associations.');
END;
/

COMMENT ON TABLE SYS_SYSTEM_SCREEN_FEATURE IS 'Hybrid Many-to-Many table linking Features to Systems and optionally Screens';
COMMENT ON COLUMN SYS_SYSTEM_SCREEN_FEATURE.SYSTEM_ID IS 'System containing the feature';
COMMENT ON COLUMN SYS_SYSTEM_SCREEN_FEATURE.SCREEN_ID IS 'Specific screen within system, or NULL for system-wide features';
COMMENT ON COLUMN SYS_SYSTEM_SCREEN_FEATURE.FEATURE_ID IS 'Action or feature permitted';
