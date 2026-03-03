# SECS/GEM C# Library – Development Plan

> Simple checklist to track progress. Detailed design specs are in `PROJECT_PLAN.md` and `design/`.

---

## ✅ Phase 0 – Design & Standards Study

- [x] Read & organize SEMI standards (E5, E30, E37, E40, E94)
- [x] Document OSI model → SEMI standards mapping (`design/OSI-Mapping.md`)
- [x] System overview diagram (`design/SystemOverview.puml`)
- [x] Library structure overview diagram (`design/SecsgemLibraryStructure.puml`)
- [x] Core layer detail diagram (`design/Core-Detail.puml`)
- [x] GEM equipment layer detail diagram (`design/GemEquipment-Detail.puml`)
- [x] Create solution & projects (`Secsgem.Core`, `Secsgem.Gem`, `Secsgem.Samples`, `Secsgem.Tests`)

---

## 🔲 Phase 1 – HSMS Transport (E37)

- [ ] `IHsmsConnection` interface
- [ ] `HsmsConnection` – passive (equipment) TCP listener
- [ ] `HsmsConnection` – active (host) TCP connector
- [ ] HSMS state machine: `NotConnected → NotSelected → Selected`
- [ ] HSMS control messages: SELECT, DESELECT, LINKTEST, SEPARATE
- [ ] Timers: T5, T6, T7, T8
- [ ] Raw message framing: encode/decode HSMS header (10 bytes)
- [ ] Basic send/receive loop
- [ ] **Demo**: Equipment ↔ Host HSMS connect + LINKTEST

---

## 🔲 Phase 2 – SECS-II Data Model & Codec (E5)

- [ ] `SecsFormat` enum (all 15 format codes)
- [ ] `SecsItem` base class + all 15 concrete types
  - [ ] `SecsList`, `SecsBoolean`
  - [ ] `SecsBinary`, `SecsAscii`, `SecsJis`
  - [ ] `SecsInt1/2/4/8`, `SecsUint1/2/4/8`
  - [ ] `SecsFloat4`, `SecsFloat8`
- [ ] `SecsHeader` – encode/decode (stream, function, W-bit, device ID, system bytes)
- [ ] `SecsMessage` – compose header + root item
- [ ] `ISecsCodec` / `SecsCodec` – encode `SecsMessage` → bytes / bytes → `SecsMessage`
- [ ] Validation: length limits, nested structure checks
- [ ] (Optional) SML parser & formatter
- [ ] **Tests**: round-trip encode/decode for every item type

---

## 🔲 Phase 3 – SECS Session API

- [ ] `ISecsSession` / `SecsSession`
  - [ ] `SendAndWaitAsync(primary)` – correlate request/reply by system bytes
  - [ ] `SendAsync(message)` – fire-and-forget (no W-bit)
  - [ ] T3 reply timeout handling
  - [ ] Unsolicited message event / callback
- [ ] `SecsTransaction` – pair primary + secondary message
- [ ] `SecsSessionOptions` – T3 timeout, device ID, etc.
- [ ] **Demo**: `S1F1` Are-You-There → `S1F2` On-Line Data exchange

---

## 🔲 Phase 4 – GEM Equipment Core (E30)

### 4a – State Models
- [ ] Communication State Model (Communicating / Not Communicating)
- [ ] Control State Model (Offline / Online-Local / Online-Remote)
- [ ] State change events exposed via `IGemEquipment`

### 4b – GEM Model Definitions
- [ ] `EquipmentConstantDefinition` (ECID, type, min/max, default, units)
- [ ] `StatusVariableDefinition` (SVID, type, units)
- [ ] `DataVariableDefinition` (DVID, type, units)
- [ ] `CollectionEventDefinition` (CEID, name)
- [ ] `ReportDefinition` (RPTID → list of VIDs)
- [ ] `AlarmDefinition` (ALID, severity, description, CEID set/clear)
- [ ] `RemoteCommandDefinition` (RCMD, parameters)

### 4c – Services
- [ ] `DataCollectionService` – report linking, enable/disable, S6F11 trigger
- [ ] `AlarmService` – SetAlarm/ClearAlarm, S5F1/S5F2, S5F3/S5F4
- [ ] `RemoteCommandService` – S2F41/S2F42 handling + application callbacks
- [ ] `EquipmentConstantService` – S2F13/S2F14, S2F15/S2F16
- [ ] `StatusVariableService` – S1F3/S1F4 handling
- [ ] `ProcessProgramService` – PP-SELECT, PP-SEND, PP-DELETE (S7 messages)

### 4d – Facade
- [ ] `IGemEquipment` interface
- [ ] `GemEquipmentSession` – wires all services + state models together
- [ ] `GemConfiguration` / `IGemConfigProvider` – load definitions from code or file

### 4e – Demo
- [ ] Equipment simulator: changing variables (temp, pressure)
- [ ] Trigger alarms and clear them
- [ ] Event-driven S6F11 reports
- [ ] Remote command (e.g., START / STOP)

---

## 🔲 Phase 5 – Quality & Hardening

- [ ] Unit tests for all `SecsItem` types and codec
- [ ] Unit tests for HSMS state machine
- [ ] Unit tests for GEM state models
- [ ] Integration test: Equipment simulator ↔ loopback host
- [ ] Structured logging throughout (ILogger-style)
- [ ] NuGet packaging (`Secsgem.Core`, `Secsgem.Gem`)
- [ ] README / API documentation

---

## 🔲 Future (After Equipment Library is Stable)

- [ ] Host-side GEM API (`GemHostSession`, `IGemHost`)
- [ ] Shared JSON/XML configuration model
- [ ] GEM300 extensions (E87 Carrier Mgmt, E90 Substrate Tracking, E94 Control Jobs, E40 Process Jobs)
- [ ] SECS-I (E4) transport over serial (RS-232)

---

*Last updated: 2026-03-03*
