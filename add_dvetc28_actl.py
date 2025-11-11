import sqlite3
from datetime import datetime, timedelta

conn = sqlite3.connect("coordinator.db")
cursor = conn.cursor()

# Check existing batch members for DVETC28
print("Current DC_Batch records for DVETC28 (Step 4):")
cursor.execute("""
    SELECT b.Id, b.BatchId, b.CarrierId, b.EqpId, b.Step, b.PPID, b.IsProcessed, m.LotId
    FROM DC_Batch b
    JOIN DC_BatchMembers m ON b.BatchId = m.BatchId AND b.CarrierId = m.CarrierId
    WHERE b.EqpId = 'DVETC28'
    ORDER BY b.Id
""")
rows = cursor.fetchall()
print(f"{'Id':<5} {'BatchId':<20} {'CarrierId':<12} {'EqpId':<10} {'Step':<6} {'PPID':<10} {'IsProcessed':<12} {'LotId':<15}")
print("-" * 110)
for row in rows:
    print(f"{row[0]:<5} {row[1]:<20} {row[2]:<12} {row[3]:<10} {row[4]:<6} {row[5]:<10} {row[6]:<12} {row[7]:<15}")

# Get the LotIds to add to DC_Actl
lot_ids = [row[7] for row in rows]
print(f"\nLotIds to add to DC_Actl for DVETC28: {lot_ids}")

# Add these lots to DC_Actl as "In Process" (3 hours ago)
now = datetime.now()
process_time = now - timedelta(hours=3)

print(f"\nAdding {len(lot_ids)} lots to DC_Actl for DVETC28...")
for lot_id in lot_ids:
    cursor.execute("""
        INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime)
        VALUES (?, ?, ?, ?)
    """, ("DVETC28", lot_id, "PS", process_time.strftime("%Y-%m-%d %H:%M:%S")))
    print(f"  - Added: DVETC28, {lot_id}, PS, {process_time.strftime('%Y-%m-%d %H:%M:%S')}")

conn.commit()

# Verify the additions
print("\nVerifying DC_Actl records for DVETC28:")
cursor.execute("""
    SELECT EqpId, LotId, LotType, TrackInTime
    FROM DC_Actl
    WHERE EqpId = 'DVETC28'
    ORDER BY TrackInTime
""")
rows = cursor.fetchall()
print(f"{'EqpId':<10} {'LotId':<15} {'LotType':<10} {'TrackInTime':<25}")
print("-" * 70)
for row in rows:
    print(f"{row[0]:<10} {row[1]:<15} {row[2]:<10} {row[3]:<25}")

conn.close()

print("\n✅ Successfully added batch member lots to DC_Actl for DVETC28")
print("\n次の手順:")
print("1. ブラウザでWorkProgress画面をリロードしてください")
print("2. DVETC28の処理中に追加したロット（JM86146.1, JM86147.1）が表示されることを確認")
print("3. その後、DC_BatchのIsProcessedがtrueになることを確認してください")
