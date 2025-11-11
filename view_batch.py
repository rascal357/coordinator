import sqlite3
import os

db_path = "coordinator.db"

if not os.path.exists(db_path):
    print(f"Database file not found: {db_path}")
    exit(1)

conn = sqlite3.connect(db_path)
cursor = conn.cursor()

cursor.execute("SELECT * FROM DC_Batch ORDER BY Id")
rows = cursor.fetchall()

print("DC_Batch Table Contents:")
print("=" * 120)
print(f"Total Records: {len(rows)}\n")

if rows:
    print(f"{'Id':<5} {'BatchId':<20} {'Step':<6} {'CarrierId':<12} {'EqpId':<10} {'PPID':<10} {'IsProcessed':<12} {'CreatedAt':<20}")
    print("-" * 120)

    for row in rows:
        id_val, batch_id, step, carrier_id, eqp_id, ppid, is_processed, created_at = row
        print(f"{id_val:<5} {batch_id:<20} {step:<6} {carrier_id:<12} {eqp_id:<10} {ppid:<10} {is_processed:<12} {created_at:<20}")
else:
    print("No records found in DC_Batch table.")

conn.close()
