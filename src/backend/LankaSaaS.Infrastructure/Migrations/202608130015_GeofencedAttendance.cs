using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LankaSaaS.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608130015_GeofencedAttendance")]
public sealed class GeofencedAttendance:Migration
{
    protected override void Up(MigrationBuilder m)=>m.Sql("""
ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "RequireLocationForAttendance" boolean NOT NULL DEFAULT false;
ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "Latitude" double precision NULL;
ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "Longitude" double precision NULL;
ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "AttendanceRadiusMeters" integer NOT NULL DEFAULT 150;
ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "MaximumLocationAccuracyMeters" integer NOT NULL DEFAULT 100;
ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "CheckInWindowMinutes" integer NOT NULL DEFAULT 60;

CREATE TABLE IF NOT EXISTS "AttendanceAttempts" (
    "Id" uuid PRIMARY KEY,
    "TenantId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "EventStaffAssignmentId" uuid NOT NULL,
    "StaffUserId" uuid NOT NULL,
    "RequestedByUserId" uuid NOT NULL,
    "Action" text NOT NULL,
    "Latitude" double precision NULL,
    "Longitude" double precision NULL,
    "AccuracyMeters" double precision NULL,
    "DistanceMeters" double precision NULL,
    "IsAccepted" boolean NOT NULL,
    "IsOverride" boolean NOT NULL,
    "OverrideReason" text NULL,
    "FailureReason" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_AttendanceAttempts_EventStaffAssignmentId_CreatedAt" ON "AttendanceAttempts" ("EventStaffAssignmentId","CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_AttendanceAttempts_EventId_CreatedAt" ON "AttendanceAttempts" ("EventId","CreatedAt");
""");

    protected override void Down(MigrationBuilder m)=>m.Sql("""
DROP TABLE IF EXISTS "AttendanceAttempts";
ALTER TABLE "Events" DROP COLUMN IF EXISTS "CheckInWindowMinutes";
ALTER TABLE "Events" DROP COLUMN IF EXISTS "MaximumLocationAccuracyMeters";
ALTER TABLE "Events" DROP COLUMN IF EXISTS "AttendanceRadiusMeters";
ALTER TABLE "Events" DROP COLUMN IF EXISTS "Longitude";
ALTER TABLE "Events" DROP COLUMN IF EXISTS "Latitude";
ALTER TABLE "Events" DROP COLUMN IF EXISTS "RequireLocationForAttendance";
""");
}
