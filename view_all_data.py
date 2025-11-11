import sqlite3

conn = sqlite3.connect("coordinator.db")
cursor = conn.cursor()

print("=" * 120)
print("DC_Wips Table - WIP情報")
print("=" * 120)
cursor.execute("SELECT Carrier, LotId, Technology, Qty, TargetEqpId, TargetPPID FROM DC_Wips ORDER BY TargetEqpId, Carrier")
rows = cursor.fetchall()
print(f"{'Carrier':<10} {'LotId':<15} {'Technology':<10} {'Qty':<5} {'TargetEqpId':<12} {'TargetPPID':<12}")
print("-" * 120)
for row in rows:
    print(f"{row[0]:<10} {row[1]:<15} {row[2]:<10} {row[3]:<5} {row[4]:<12} {row[5]:<12}")

print("\n" + "=" * 120)
print("DC_Actl Table - 実績処理データ")
print("=" * 120)
cursor.execute("SELECT EqpId, LotId, LotType, TrackInTime FROM DC_Actl ORDER BY EqpId, TrackInTime")
rows = cursor.fetchall()
print(f"{'EqpId':<10} {'LotId':<15} {'LotType':<10} {'TrackInTime':<25}")
print("-" * 120)
for row in rows:
    print(f"{row[0]:<10} {row[1]:<15} {row[2]:<10} {row[3]:<25}")

print("\n" + "=" * 120)
print("DC_Batch Table - バッチ情報（サマリー）")
print("=" * 120)
cursor.execute("SELECT BatchId, COUNT(*) as Records, GROUP_CONCAT(DISTINCT CarrierId) as Carriers, CreatedAt FROM DC_Batch GROUP BY BatchId ORDER BY CreatedAt")
rows = cursor.fetchall()
print(f"{'BatchId':<20} {'Records':<10} {'Carriers':<30} {'CreatedAt':<25}")
print("-" * 120)
for row in rows:
    print(f"{row[0]:<20} {row[1]:<10} {row[2]:<30} {row[3]:<25}")

print("\n" + "=" * 120)
print("装置別のデータ状況")
print("=" * 120)
cursor.execute("""
    SELECT
        e.Name,
        e.Type,
        e.Line,
        (SELECT COUNT(*) FROM DC_Wips w WHERE w.TargetEqpId = e.Name) as WIP_Count,
        (SELECT COUNT(*) FROM DC_Actl a WHERE a.EqpId = e.Name) as Actl_Count,
        (SELECT COUNT(DISTINCT BatchId) FROM DC_Batch b WHERE b.EqpId = e.Name AND Step = 1) as Batch_Count
    FROM DC_Eqps e
    ORDER BY e.Type, e.Name
""")
rows = cursor.fetchall()
print(f"{'装置名':<10} {'TYPE':<10} {'LINE':<6} {'WIP':<6} {'実績':<6} {'バッチ':<8}")
print("-" * 120)
for row in rows:
    print(f"{row[0]:<10} {row[1]:<10} {row[2]:<6} {row[3]:<6} {row[4]:<6} {row[5]:<8}")

conn.close()
