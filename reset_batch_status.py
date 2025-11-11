import sqlite3

conn = sqlite3.connect("coordinator.db")
cursor = conn.cursor()

print("Current DC_Batch status:")
cursor.execute("SELECT Id, BatchId, Step, CarrierId, EqpId, IsProcessed FROM DC_Batch ORDER BY Id")
rows = cursor.fetchall()
for row in rows:
    status = "TRUE" if row[5] else "FALSE"
    print(f"Id: {row[0]}, Step: {row[2]}, CarrierId: {row[3]}, EqpId: {row[4]}, IsProcessed: {status}")

print("\n" + "="*80)
print("Updating IsProcessed to FALSE for all records except DVETC28...")
print("="*80)

# Update IsProcessed to FALSE for all records where EqpId is not DVETC28
cursor.execute("""
    UPDATE DC_Batch
    SET IsProcessed = 0
    WHERE EqpId != 'DVETC28'
""")

rows_updated = cursor.rowcount
conn.commit()

print(f"\n✅ Updated {rows_updated} records\n")

print("Updated DC_Batch status:")
cursor.execute("SELECT Id, BatchId, Step, CarrierId, EqpId, IsProcessed FROM DC_Batch ORDER BY Id")
rows = cursor.fetchall()
for row in rows:
    status = "✅ TRUE" if row[5] else "❌ FALSE"
    print(f"Id: {row[0]}, Step: {row[2]}, CarrierId: {row[3]}, EqpId: {row[4]}, IsProcessed: {status}")

conn.close()

print("\n" + "="*80)
print("Summary:")
print("- DVETC25, DVETC26, DVETC27: IsProcessed = FALSE (予約1に表示されます)")
print("- DVETC28: IsProcessed = TRUE (処理済みのため予約1には表示されません)")
print("="*80)
