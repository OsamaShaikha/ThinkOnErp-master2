using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceGeofencing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SYS_BRANCH columns (via raw SQL if table exists)
            migrationBuilder.Sql(@"
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE SYS_BRANCH ADD (
        LATITUDE NUMBER(10, 7) NULL,
        LONGITUDE NUMBER(11, 7) NULL,
        GEOFENCE_RADIUS_METERS NUMBER(10, 2) DEFAULT 100.00 NOT NULL,
        ENFORCE_GEOFENCE CHAR(1) DEFAULT ''1'' NOT NULL
    )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -1430 THEN -- ORA-01430: column being added already exists in table
            NULL;
        END IF;
END;");

            // HR_RAW_ATTENDANCE columns
            migrationBuilder.AddColumn<long>(
                name: "BRANCH_ID",
                table: "HR_RAW_ATTENDANCE",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LATITUDE",
                table: "HR_RAW_ATTENDANCE",
                type: "DECIMAL(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LONGITUDE",
                table: "HR_RAW_ATTENDANCE",
                type: "DECIMAL(11,7)",
                precision: 11,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ACCURACY_METERS",
                table: "HR_RAW_ATTENDANCE",
                type: "DECIMAL(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DISTANCE_METERS",
                table: "HR_RAW_ATTENDANCE",
                type: "DECIMAL(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IS_WITHIN_GEOFENCE",
                table: "HR_RAW_ATTENDANCE",
                type: "NUMBER(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GEOFENCE_STATUS",
                table: "HR_RAW_ATTENDANCE",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            // HR_ATTENDANCE_DAY columns
            migrationBuilder.AddColumn<long>(
                name: "CHECK_IN_BRANCH_ID",
                table: "HR_ATTENDANCE_DAY",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CHECK_IN_LATITUDE",
                table: "HR_ATTENDANCE_DAY",
                type: "DECIMAL(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CHECK_IN_LONGITUDE",
                table: "HR_ATTENDANCE_DAY",
                type: "DECIMAL(11,7)",
                precision: 11,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CHECK_IN_DISTANCE_METERS",
                table: "HR_ATTENDANCE_DAY",
                type: "DECIMAL(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IS_CHECK_IN_WITHIN_GEOFENCE",
                table: "HR_ATTENDANCE_DAY",
                type: "NUMBER(1)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CHECK_OUT_BRANCH_ID",
                table: "HR_ATTENDANCE_DAY",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CHECK_OUT_LATITUDE",
                table: "HR_ATTENDANCE_DAY",
                type: "DECIMAL(10,7)",
                precision: 10,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CHECK_OUT_LONGITUDE",
                table: "HR_ATTENDANCE_DAY",
                type: "DECIMAL(11,7)",
                precision: 11,
                scale: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CHECK_OUT_DISTANCE_METERS",
                table: "HR_ATTENDANCE_DAY",
                type: "DECIMAL(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IS_CHECK_OUT_WITHIN_GEOFENCE",
                table: "HR_ATTENDANCE_DAY",
                type: "NUMBER(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HAS_GEOFENCE_VIOLATION",
                table: "HR_ATTENDANCE_DAY",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            // HR_ATTENDANCE_POLICY columns
            migrationBuilder.AddColumn<bool>(
                name: "ENFORCE_GEOFENCE",
                table: "HR_ATTENDANCE_POLICY",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DEFAULT_RADIUS_METERS",
                table: "HR_ATTENDANCE_POLICY",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "GEOFENCE_VIOLATION_ACTION",
                table: "HR_ATTENDANCE_POLICY",
                type: "NVARCHAR2(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "REJECT");

            migrationBuilder.AddColumn<bool>(
                name: "ALLOW_ANY_BRANCH_PUNCH",
                table: "HR_ATTENDANCE_POLICY",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BRANCH_ID", table: "HR_RAW_ATTENDANCE");
            migrationBuilder.DropColumn(name: "LATITUDE", table: "HR_RAW_ATTENDANCE");
            migrationBuilder.DropColumn(name: "LONGITUDE", table: "HR_RAW_ATTENDANCE");
            migrationBuilder.DropColumn(name: "ACCURACY_METERS", table: "HR_RAW_ATTENDANCE");
            migrationBuilder.DropColumn(name: "DISTANCE_METERS", table: "HR_RAW_ATTENDANCE");
            migrationBuilder.DropColumn(name: "IS_WITHIN_GEOFENCE", table: "HR_RAW_ATTENDANCE");
            migrationBuilder.DropColumn(name: "GEOFENCE_STATUS", table: "HR_RAW_ATTENDANCE");

            migrationBuilder.DropColumn(name: "CHECK_IN_BRANCH_ID", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_IN_LATITUDE", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_IN_LONGITUDE", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_IN_DISTANCE_METERS", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "IS_CHECK_IN_WITHIN_GEOFENCE", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_OUT_BRANCH_ID", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_OUT_LATITUDE", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_OUT_LONGITUDE", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "CHECK_OUT_DISTANCE_METERS", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "IS_CHECK_OUT_WITHIN_GEOFENCE", table: "HR_ATTENDANCE_DAY");
            migrationBuilder.DropColumn(name: "HAS_GEOFENCE_VIOLATION", table: "HR_ATTENDANCE_DAY");

            migrationBuilder.DropColumn(name: "ENFORCE_GEOFENCE", table: "HR_ATTENDANCE_POLICY");
            migrationBuilder.DropColumn(name: "DEFAULT_RADIUS_METERS", table: "HR_ATTENDANCE_POLICY");
            migrationBuilder.DropColumn(name: "GEOFENCE_VIOLATION_ACTION", table: "HR_ATTENDANCE_POLICY");
            migrationBuilder.DropColumn(name: "ALLOW_ANY_BRANCH_PUNCH", table: "HR_ATTENDANCE_POLICY");
        }
    }
}
