using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgyptOnline.Migrations
{
    /// <inheritdoc />
    public partial class AddSimpleContractAndWalletTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='WithdrawRequests'
                        AND column_name='SourceWalletNumber')
                    THEN
                        ALTER TABLE ""WithdrawRequests""
                        ADD ""SourceWalletNumber"" character varying(100) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='UserWallets'
                        AND column_name='WalletNumber')
                    THEN
                        ALTER TABLE ""UserWallets""
                        ADD ""WalletNumber"" character varying(50) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='DepositRequests'
                        AND column_name='RecipientPhoneNumber')
                    THEN
                        ALTER TABLE ""DepositRequests""
                        ADD ""RecipientPhoneNumber"" character varying(100) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='DepositRequests'
                        AND column_name='WalletOwnerName')
                    THEN
                        ALTER TABLE ""DepositRequests""
                        ADD ""WalletOwnerName"" character varying(200) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='CheckInDate')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""CheckInDate"" date;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='CheckInStatus')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""CheckInStatus"" character varying(50) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='CheckInTime')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""CheckInTime"" timestamptz;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='ClientPenaltyPaid')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""ClientPenaltyPaid"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='ClientTerminationRequested')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""ClientTerminationRequested"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='ClientUserId')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""ClientUserId"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='DailySalary')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""DailySalary"" numeric(18,2) NOT NULL DEFAULT 0;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='DaysWorked')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""DaysWorked"" integer NOT NULL DEFAULT 0;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='DurationDays')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""DurationDays"" integer NOT NULL DEFAULT 0;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='IsSimpleContract')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""IsSimpleContract"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='Notes')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""Notes"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='WorkerPenaltyPaid')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""WorkerPenaltyPaid"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='WorkerTerminationRequested')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""WorkerTerminationRequested"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='WorkerUserId')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""WorkerUserId"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='Contracts' AND column_name='WorkplaceAddress')
                    THEN
                        ALTER TABLE ""Contracts"" ADD ""WorkplaceAddress"" character varying(500) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.columns
                        WHERE table_name='AttendanceRecords' AND column_name='CheckInTime')
                    THEN
                        ALTER TABLE ""AttendanceRecords"" ADD ""CheckInTime"" timestamptz;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname='IX_Contracts_ClientUserId')
                    THEN
                        CREATE INDEX ""IX_Contracts_ClientUserId"" ON ""Contracts"" (""ClientUserId"");
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM pg_indexes
                        WHERE indexname='IX_Contracts_WorkerUserId')
                    THEN
                        CREATE INDEX ""IX_Contracts_WorkerUserId"" ON ""Contracts"" (""WorkerUserId"");
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name='FK_Contracts_AspNetUsers_ClientUserId')
                    THEN
                        ALTER TABLE ""Contracts""
                        ADD CONSTRAINT ""FK_Contracts_AspNetUsers_ClientUserId""
                        FOREIGN KEY (""ClientUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM information_schema.table_constraints
                        WHERE constraint_name='FK_Contracts_AspNetUsers_WorkerUserId')
                    THEN
                        ALTER TABLE ""Contracts""
                        ADD CONSTRAINT ""FK_Contracts_AspNetUsers_WorkerUserId""
                        FOREIGN KEY (""WorkerUserId"") REFERENCES ""AspNetUsers""(""Id"") ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ClientUserId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ClientUserId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "SourceWalletNumber",
                table: "WithdrawRequests");

            migrationBuilder.DropColumn(
                name: "WalletNumber",
                table: "UserWallets");

            migrationBuilder.DropColumn(
                name: "RecipientPhoneNumber",
                table: "DepositRequests");

            migrationBuilder.DropColumn(
                name: "WalletOwnerName",
                table: "DepositRequests");

            migrationBuilder.DropColumn(
                name: "CheckInDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInStatus",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClientPenaltyPaid",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClientTerminationRequested",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ClientUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DailySalary",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DaysWorked",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsSimpleContract",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerPenaltyPaid",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerTerminationRequested",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkerUserId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "WorkplaceAddress",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CheckInTime",
                table: "AttendanceRecords");
        }
    }
}