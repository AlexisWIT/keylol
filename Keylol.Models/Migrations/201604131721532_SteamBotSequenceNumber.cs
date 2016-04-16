namespace Keylol.Models.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SteamBotSequenceNumber : DbMigration
    {
        public override void Up()
        {
            Sql("CREATE SEQUENCE [dbo].[SteamBotSequence] AS int START WITH 1 INCREMENT BY 1 NO CACHE");
            AddColumn("dbo.SteamBots", "SequenceNumber", c => c.Int(nullable: false, defaultValueSql: "NEXT VALUE FOR [dbo].[SteamBotSequence]"));
            CreateIndex("dbo.SteamBots", "SequenceNumber", unique: true);

            // ��Ϊ���� Clustered Index ���ڸ��ӣ��˴���Ҫ�˹��ֶ��� SSMS �е��� SteamBot ��� Clustered Index �� SequenceNumber ��
        }

        public override void Down()
        {
            DropIndex("dbo.SteamBots", new[] { "SequenceNumber" });
            DropColumn("dbo.SteamBots", "SequenceNumber");
            Sql("DROP SEQUENCE [dbo].[SteamBotSequence]");
        }
    }
}
