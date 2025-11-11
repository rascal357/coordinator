import sqlite3
from datetime import datetime, timedelta

conn = sqlite3.connect("coordinator.db")
cursor = conn.cursor()

# Get existing equipment
cursor.execute("SELECT Name FROM DC_Eqps")
equipments = [row[0] for row in cursor.fetchall()]
print(f"Found {len(equipments)} equipments: {equipments}\n")

# Create test data for WIPs
test_wips = [
    # DVETC26用のWIPデータ
    {
        "Priority": 3,
        "Technology": "T8-HV",
        "Carrier": "C30001",
        "LotId": "LT30001.1",
        "Qty": 24,
        "PartName": "WA0050-FN50-V-S-2",
        "CurrentStage": "BL-OX",
        "CurrentStep": "OX01",
        "TargetStage": "G-SIO",
        "TargetStep": "FDP02",
        "TargetEqpId": "DVETC26",
        "TargetPPID": "GSIO5F2"
    },
    {
        "Priority": 4,
        "Technology": "T8-HV",
        "Carrier": "C30002",
        "LotId": "LT30002.1",
        "Qty": 24,
        "PartName": "WA0050-FN50-V-S-2",
        "CurrentStage": "BL-OX",
        "CurrentStep": "OX01",
        "TargetStage": "G-SIO",
        "TargetStep": "FDP02",
        "TargetEqpId": "DVETC26",
        "TargetPPID": "GSIO5F2"
    },
    # DVETC27用のWIPデータ
    {
        "Priority": 2,
        "Technology": "T9-MV",
        "Carrier": "C40001",
        "LotId": "LT40001.1",
        "Qty": 25,
        "PartName": "WA0060-FN60-V-S-3",
        "CurrentStage": "BL-NI",
        "CurrentStep": "NI01",
        "TargetStage": "G-SIO",
        "TargetStep": "FDP03",
        "TargetEqpId": "DVETC27",
        "TargetPPID": "GSIO7F3"
    },
    # DVETC38用のWIPデータ（すでにActlに登録されているロット用）
    {
        "Priority": 1,
        "Technology": "T10-PS",
        "Carrier": "C50001",
        "LotId": "SY79874.1",
        "Qty": 23,
        "PartName": "WA0070-FN70-V-S-4",
        "CurrentStage": "BL-POLY",
        "CurrentStep": "POLY01",
        "TargetStage": "G-POLY",
        "TargetStep": "FDP04",
        "TargetEqpId": "DVETC38",
        "TargetPPID": "GPOLY2F1"
    },
    {
        "Priority": 1,
        "Technology": "T10-PS",
        "Carrier": "C50002",
        "LotId": "SY79872.1",
        "Qty": 23,
        "PartName": "WA0070-FN70-V-S-4",
        "CurrentStage": "BL-POLY",
        "CurrentStep": "POLY01",
        "TargetStage": "G-POLY",
        "TargetStep": "FDP04",
        "TargetEqpId": "DVETC38",
        "TargetPPID": "GPOLY2F1"
    },
    {
        "Priority": 1,
        "Technology": "T10-PS",
        "Carrier": "C50003",
        "LotId": "SY79906.1",
        "Qty": 23,
        "PartName": "WA0070-FN70-V-S-4",
        "CurrentStage": "BL-POLY",
        "CurrentStep": "POLY01",
        "TargetStage": "G-POLY",
        "TargetStep": "FDP04",
        "TargetEqpId": "DVETC38",
        "TargetPPID": "GPOLY2F1"
    },
    # DVETC39用のWIPデータ
    {
        "Priority": 2,
        "Technology": "T11-HV",
        "Carrier": "C60001",
        "LotId": "LT60001.1",
        "Qty": 25,
        "PartName": "WA0080-FN80-V-S-5",
        "CurrentStage": "BL-TI",
        "CurrentStep": "TI01",
        "TargetStage": "G-POLY",
        "TargetStep": "FDP05",
        "TargetEqpId": "DVETC39",
        "TargetPPID": "GPOLY3F2"
    },
]

print("Inserting WIP data...")
for wip in test_wips:
    cursor.execute("""
        INSERT INTO DC_Wips (Priority, Technology, Carrier, LotId, Qty, PartName,
                             CurrentStage, CurrentStep, TargetStage, TargetStep,
                             TargetEqpId, TargetPPID)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, (wip["Priority"], wip["Technology"], wip["Carrier"], wip["LotId"],
          wip["Qty"], wip["PartName"], wip["CurrentStage"], wip["CurrentStep"],
          wip["TargetStage"], wip["TargetStep"], wip["TargetEqpId"], wip["TargetPPID"]))

print(f"Inserted {len(test_wips)} WIP records\n")

# Create test data for Actual processing (DC_Actl)
now = datetime.now()
test_actls = [
    # DVETC26 - 処理中グループ（3時間前）
    {"EqpId": "DVETC26", "LotId": "LT20001.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=3)},
    {"EqpId": "DVETC26", "LotId": "LT20002.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=3)},
    {"EqpId": "DVETC26", "LotId": "LT20003.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=3, minutes=2)},
    {"EqpId": "DVETC26", "LotId": "LT20004.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=3, minutes=3)},

    # DVETC26 - 処理待ちグループ（15分前）
    {"EqpId": "DVETC26", "LotId": "LT20005.1", "LotType": "PS", "TrackInTime": now - timedelta(minutes=15)},
    {"EqpId": "DVETC26", "LotId": "LT20006.1", "LotType": "PS", "TrackInTime": now - timedelta(minutes=15)},
    {"EqpId": "DVETC26", "LotId": "LT20007.1", "LotType": "PS", "TrackInTime": now - timedelta(minutes=16)},

    # DVETC27 - 処理中グループ（2時間前）
    {"EqpId": "DVETC27", "LotId": "LT30001.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=2)},
    {"EqpId": "DVETC27", "LotId": "LT30002.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=2, minutes=1)},
    {"EqpId": "DVETC27", "LotId": "LT30003.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=2, minutes=2)},

    # DVETC27 - 処理待ちグループ（10分前）
    {"EqpId": "DVETC27", "LotId": "LT30004.1", "LotType": "PS", "TrackInTime": now - timedelta(minutes=10)},
    {"EqpId": "DVETC27", "LotId": "LT30005.1", "LotType": "PS", "TrackInTime": now - timedelta(minutes=11)},

    # DVETC39 - 処理中グループ（1.5時間前）
    {"EqpId": "DVETC39", "LotId": "LT40001.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=1.5)},
    {"EqpId": "DVETC39", "LotId": "LT40002.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=1.5, minutes=2)},
    {"EqpId": "DVETC39", "LotId": "LT40003.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=1.5, minutes=3)},
    {"EqpId": "DVETC39", "LotId": "LT40004.1", "LotType": "PS", "TrackInTime": now - timedelta(hours=1.5, minutes=4)},
]

print("Inserting Actual processing data...")
for actl in test_actls:
    cursor.execute("""
        INSERT INTO DC_Actl (EqpId, LotId, LotType, TrackInTime)
        VALUES (?, ?, ?, ?)
    """, (actl["EqpId"], actl["LotId"], actl["LotType"], actl["TrackInTime"].strftime("%Y-%m-%d %H:%M:%S")))

print(f"Inserted {len(test_actls)} Actual processing records\n")

# Create carrier steps for new WIPs
carrier_steps = [
    # C30001のステップ
    {"Carrier": "C30001", "Qty": 24, "Step": 1, "EqpId": "DVETC26", "PPID": "PPID_C30001_1"},
    {"Carrier": "C30001", "Qty": 24, "Step": 2, "EqpId": "DVETC27", "PPID": "PPID_C30001_2"},
    {"Carrier": "C30001", "Qty": 24, "Step": 3, "EqpId": "DVETC28", "PPID": "PPID_C30001_3"},

    # C30002のステップ
    {"Carrier": "C30002", "Qty": 24, "Step": 1, "EqpId": "DVETC26", "PPID": "PPID_C30002_1"},
    {"Carrier": "C30002", "Qty": 24, "Step": 2, "EqpId": "DVETC27", "PPID": "PPID_C30002_2"},
    {"Carrier": "C30002", "Qty": 24, "Step": 3, "EqpId": "DVETC28", "PPID": "PPID_C30002_3"},

    # C40001のステップ
    {"Carrier": "C40001", "Qty": 25, "Step": 1, "EqpId": "DVETC27", "PPID": "PPID_C40001_1"},
    {"Carrier": "C40001", "Qty": 25, "Step": 2, "EqpId": "DVETC28", "PPID": "PPID_C40001_2"},

    # C60001のステップ
    {"Carrier": "C60001", "Qty": 25, "Step": 1, "EqpId": "DVETC39", "PPID": "PPID_C60001_1"},
    {"Carrier": "C60001", "Qty": 25, "Step": 2, "EqpId": "DVETC38", "PPID": "PPID_C60001_2"},
]

print("Inserting Carrier Steps data...")
for cs in carrier_steps:
    cursor.execute("""
        INSERT INTO DC_CarrierSteps (Carrier, Qty, Step, EqpId, PPID)
        VALUES (?, ?, ?, ?, ?)
    """, (cs["Carrier"], cs["Qty"], cs["Step"], cs["EqpId"], cs["PPID"]))

print(f"Inserted {len(carrier_steps)} Carrier Steps records\n")

conn.commit()
conn.close()

print("✅ Test data insertion completed successfully!")
print("\nSummary:")
print(f"  - {len(test_wips)} WIP records")
print(f"  - {len(test_actls)} Actual processing records")
print(f"  - {len(carrier_steps)} Carrier Steps records")
