import sqlite3

conn = sqlite3.connect("coordinator.db")
cursor = conn.cursor()

print("=" * 120)
print("DC_Batch Status Check - BatchId: 20251111215740745")
print("=" * 120)

cursor.execute("""
    SELECT b.Id, b.BatchId, b.Step, b.CarrierId, b.EqpId, b.PPID, b.IsProcessed, m.LotId
    FROM DC_Batch b
    JOIN DC_BatchMembers m ON b.BatchId = m.BatchId AND b.CarrierId = m.CarrierId
    WHERE b.BatchId = '20251111215740745'
    ORDER BY b.Step, b.CarrierId
""")
rows = cursor.fetchall()

print(f"{'Id':<5} {'BatchId':<20} {'Step':<6} {'CarrierId':<12} {'EqpId':<10} {'PPID':<10} {'IsProcessed':<12} {'LotId':<15}")
print("-" * 120)
for row in rows:
    is_processed_str = "✅ TRUE" if row[6] else "❌ FALSE"
    print(f"{row[0]:<5} {row[1]:<20} {row[2]:<6} {row[3]:<12} {row[4]:<10} {row[5]:<10} {is_processed_str:<12} {row[7]:<15}")

# Count processed vs not processed
cursor.execute("""
    SELECT IsProcessed, COUNT(*)
    FROM DC_Batch
    WHERE BatchId = '20251111215740745'
    GROUP BY IsProcessed
""")
status_counts = cursor.fetchall()

print("\n" + "=" * 120)
print("Summary:")
print("-" * 120)
for status, count in status_counts:
    status_str = "IsProcessed = TRUE" if status else "IsProcessed = FALSE"
    print(f"{status_str}: {count} records")

conn.close()
