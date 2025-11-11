# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Coordinator is a semiconductor manufacturing furnace processing management system. It manages work-in-progress (WIP) tracking, batch creation, and equipment scheduling for furnace operations.

**Technology Stack:**
- Language: C#
- Framework: Razor Pages
- Database: SQLite

## System Architecture

### Database Schema

The system uses SQLite with the following core tables:

**DC_Eqps** - Equipment information
- Fields: ID, NAME, TYPE, LINE (A or B)
- Equipment names and types for filtering and display

**DC_Wips** - Work-in-progress lots
- Fields: Priority, Technology, Carrier, LotId, Qty, PartName, CurrentStage, CurrentStep, TargetStage, TargetStep, TargetEqpId, TargetPPID
- Tracks lots waiting for processing on specific equipment

**DC_CarrierSteps** - Carrier processing steps
- Fields: Carrier, Qty, Step (1-4), EqpId, PPID
- Defines up to 4 processing steps per carrier with equipment and recipe (PPID)

**DC_Batch** - Batch records
- Fields: Id (PK), BatchId, Step, CarrierId, EqpId, PPID, IsProcessed, CreatedAt
- Each batch can have multiple carriers and steps, all sharing the same BatchId

**DC_BatchMembers** - Batch member details
- Fields: Id (PK), BatchId, CarrierId, LotId, Qty, Technology
- Stores lot information for each carrier in a batch

**DC_Actl** - Actual processing records
- Fields: EqpId, LotId, LotType, TrackInTime
- Records of lots currently being processed or waiting

### Application Flow

1. **Dashboard** → Equipment list by TYPE and LINE, navigate to WIP Lot List
2. **WIP Lot List** → Select carriers for specific equipment, validate selection, pass to Create Batch
3. **Create Batch** → Display 1-4 steps per carrier from DC_CarrierSteps, generate unique BatchId, save to DC_Batch and DC_BatchMembers
4. **Work Progress** → Display processing status (In Process, Waiting, Reserved 1-3) grouped by equipment

### Key Business Logic

**Batch Creation:**
- Generate unique BatchId for all carriers in a batch
- Store each step (1-4) as separate records in DC_Batch
- All records with same BatchId must have identical CreatedAt timestamp
- Duplicate carrier selection is automatically eliminated

**Work Progress Grouping:**
- "In Process" / "Waiting": Group DC_Actl records by TrackInTime within ±5 minutes
  - Earlier group = In Process
  - Later group = Waiting
- "Reserved 1/2/3": DC_Batch records ordered by CreatedAt (oldest first)
- Match LotIds between DC_Actl groups and DC_Batch, set IS_PROCESSING=true to exclude from Reserved display

**UI Requirements:**
- All dynamic elements must have unique IDs for JavaScript updates
- TYPE and LINE filters on Dashboard and Work Progress screens
- Carrier selection validation with user-friendly messages

## Development Commands

This repository currently contains only the requirements specification. The C# Razor application has not yet been implemented.

When implementing:
- Create a new ASP.NET Core Razor Pages project
- Configure SQLite database connection
- Implement the database schema defined above
- Create Razor pages for: Dashboard, WipLotList, CreateBatch, WorkProgress
