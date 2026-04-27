-- =============================================
-- Script: 47_Create_Ticket_Configuration_Table.sql
-- Description: Creates SYS_TICKET_CONFIG table for storing configurable ticket system settings
-- Author: ThinkOnERP Development Team
-- Date: 2024
-- =============================================

-- Create sequence for SYS_TICKET_CONFIG
CREATE SEQUENCE SEQ_SYS_TICKET_CONFIG
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

-- Create SYS_TICKET_CONFIG table
CREATE TABLE SYS_TICKET_CONFIG (
    ROW_ID NUMBER(19) PRIMARY KEY,
    CONFIG_KEY NVARCHAR2(100) NOT NULL UNIQUE,
    CONFIG_VALUE NCLOB NOT NULL,
    CONFIG_TYPE NVARCHAR2(50) NOT NULL, -- SLA, FileAttachment, Notification, Workflow
    DESCRIPTION_AR NVARCHAR2(500) NULL,
    DESCRIPTION_EN NVARCHAR2(500) NULL,
    IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL,
    UPDATE_USER NVARCHAR2(100) NULL,
    UPDATE_DATE DATE NULL,
    
    CONSTRAINT CHK_TICKET_CONFIG_ACTIVE CHECK (IS_ACTIVE IN ('Y', 'N')),
    CONSTRAINT CHK_TICKET_CONFIG_TYPE CHECK (CONFIG_TYPE IN ('SLA', 'FileAttachment', 'Notification', 'Workflow', 'General'))
);

-- Create index for faster lookups
CREATE INDEX IDX_TICKET_CONFIG_KEY ON SYS_TICKET_CONFIG(CONFIG_KEY);
CREATE INDEX IDX_TICKET_CONFIG_TYPE ON SYS_TICKET_CONFIG(CONFIG_TYPE);
CREATE INDEX IDX_TICKET_CONFIG_ACTIVE ON SYS_TICKET_CONFIG(IS_ACTIVE);

-- Insert default configuration values
INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'SLA.Priority.Low.Hours', '72', 'SLA', 'ساعات الهدف لأولوية منخفضة', 'Target hours for Low priority', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'SLA.Priority.Medium.Hours', '24', 'SLA', 'ساعات الهدف لأولوية متوسطة', 'Target hours for Medium priority', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'SLA.Priority.High.Hours', '8', 'SLA', 'ساعات الهدف لأولوية عالية', 'Target hours for High priority', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'SLA.Priority.Critical.Hours', '2', 'SLA', 'ساعات الهدف لأولوية حرجة', 'Target hours for Critical priority', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'SLA.Escalation.Threshold.Percentage', '80', 'SLA', 'نسبة التصعيد من وقت الهدف', 'Escalation threshold percentage of target time', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'FileAttachment.MaxSizeBytes', '10485760', 'FileAttachment', 'الحد الأقصى لحجم الملف بالبايت (10 ميجابايت)', 'Maximum file size in bytes (10MB)', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'FileAttachment.MaxCount', '5', 'FileAttachment', 'الحد الأقصى لعدد المرفقات لكل تذكرة', 'Maximum number of attachments per ticket', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'FileAttachment.AllowedTypes', '.pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt', 'FileAttachment', 'أنواع الملفات المسموح بها', 'Allowed file types', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Notification.Enabled', 'true', 'Notification', 'تفعيل الإشعارات', 'Enable notifications', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Notification.Template.TicketCreated', 'New ticket #{TicketId} has been created: {Title}', 'Notification', 'قالب إشعار إنشاء تذكرة', 'Ticket created notification template', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Notification.Template.TicketAssigned', 'Ticket #{TicketId} has been assigned to you: {Title}', 'Notification', 'قالب إشعار تعيين تذكرة', 'Ticket assigned notification template', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Notification.Template.TicketStatusChanged', 'Ticket #{TicketId} status changed to {Status}: {Title}', 'Notification', 'قالب إشعار تغيير حالة التذكرة', 'Ticket status changed notification template', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Notification.Template.CommentAdded', 'New comment added to ticket #{TicketId}: {Title}', 'Notification', 'قالب إشعار إضافة تعليق', 'Comment added notification template', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Workflow.AllowedStatusTransitions', '{"Open":["InProgress","Cancelled"],"InProgress":["PendingCustomer","Resolved","Cancelled"],"PendingCustomer":["InProgress","Resolved","Cancelled"],"Resolved":["Closed"],"Closed":[],"Cancelled":[]}', 'Workflow', 'انتقالات الحالة المسموح بها', 'Allowed status transitions', 'SYSTEM');

INSERT INTO SYS_TICKET_CONFIG (ROW_ID, CONFIG_KEY, CONFIG_VALUE, CONFIG_TYPE, DESCRIPTION_AR, DESCRIPTION_EN, CREATION_USER)
VALUES (SEQ_SYS_TICKET_CONFIG.NEXTVAL, 'Workflow.AutoCloseResolvedAfterDays', '7', 'Workflow', 'إغلاق تلقائي للتذاكر المحلولة بعد أيام', 'Auto-close resolved tickets after days', 'SYSTEM');

COMMIT;

-- Display success message
SELECT 'SYS_TICKET_CONFIG table created successfully with default configuration values' AS STATUS FROM DUAL;
