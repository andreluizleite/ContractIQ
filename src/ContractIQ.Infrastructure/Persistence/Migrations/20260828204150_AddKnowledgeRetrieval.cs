using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace ContractIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    source_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    embedding_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    indexed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    section = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false, computedColumnSql: "to_tsvector('simple', coalesce(section, '') || ' ' || coalesce(content, ''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_chunks_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_chunks_document_index",
                table: "knowledge_chunks",
                columns: new[] { "document_id", "chunk_index" },
                unique: true);

            migrationBuilder.Sql(
                "CREATE INDEX ix_knowledge_chunks_search_vector " +
                "ON knowledge_chunks USING GIN (search_vector);");

            migrationBuilder.Sql(
                "CREATE INDEX ix_knowledge_chunks_embedding_hnsw " +
                "ON knowledge_chunks USING hnsw (embedding vector_cosine_ops);");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_scope_effective_from",
                table: "knowledge_documents",
                columns: new[] { "customer_id", "contract_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_documents_key_version",
                table: "knowledge_documents",
                columns: new[] { "document_key", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_documents");
        }
    }
}
