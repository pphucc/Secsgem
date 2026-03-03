## SECS/GEM C# Library – Project Plan

### 1. Project Overview

**Goal**: Build a reusable **C# .NET 8 library** implementing the SEMI **SECS-II** and **GEM** standards so semiconductor equipment and host systems can communicate in a standardized way without each project re-implementing the standards.

- **Target users**: 
  - Equipment vendors (equipment-side GEM & SECS-II implementation).
  - Host application developers (host-side GEM & SECS-II implementation, planned for a later phase).
- **Core standards (initial scope)**:
  - **SEMI E5** – SECS-II (message and data structure).
  - **SEMI E37** – HSMS (High-Speed SECS Message Services, over TCP/IP).
  - **SEMI E30** – GEM (Generic Equipment Model: state models, alarms, events, reports, remote commands).
- **Future / optional standards**:
  - **SEMI E4** – SECS-I (RS-232; optional, later).
  - GEM300-related standards (E39, E87, E90, E94, etc.) for advanced material handling (long term).
- **Current project scope**:
  - Focus on the **equipment-side library only** (transport, SECS-II, GEM equipment services).
  - Host-side GEM convenience APIs will be implemented **after** the equipment side is complete and stable.

### 2. Scope & Constraints

- **.NET target**:
  - Primary target: **.NET 8** (modern, cross-platform, long-term support).
  - Design with clean abstractions so core logic can optionally be multi-targeted to **.NET Framework 4.8** or **.NET Standard 2.0** later if needed for legacy factory environments.
- **Transport**:
  - Start with **HSMS (E37) over TCP/IP** only.
  - Consider SECS-I (E4) in a future phase.
- **Roles**:
  - **Current implementation scope**: equipment role only (equipment-side GEM & SECS-II).
  - Design abstractions so a **host role** can be added later without breaking changes.
  - Host-side GEM will be implemented in a **separate, later phase** once equipment capabilities are complete.
- **Style preference**:
  - Prefer **block structure** instead of multiple early returns in methods.

---

### 3. High-Level Architecture

Layered architecture to keep responsibilities clear and allow future evolution:

- **Layer 0 – Utilities**
  - Logging abstraction (e.g. `ILogger`-style interfaces).
  - Configuration helpers (loading JSON/XML configs).
  - Common types: timeouts, result objects, exceptions, etc.

- **Layer 1 – Transport (HSMS / SECS-I)**
  - HSMS (E37) over TCP/IP:
    - Active/passive connection.
    - HSMS control messages: SELECT/DESELECT, LINKTEST, SEPARATE.
    - State machine: NOT CONNECTED, NOT SELECTED, SELECTED.
    - Timers: T5, T6, T7, T8.
  - Transport abstractions:
    - `IHsmsConnection` interface (open/close, send, events for receive and state changes).
  - (Future) SECS-I (E4) over serial as an alternative implementation.

- **Layer 2 – SECS-II Core (E5)**
  - **Data model**:
    - Base `SecsItem` type with specific subclasses (e.g. `SecsAscii`, `SecsBinary`, `SecsInt2`, `SecsUint2`, `SecsFloat4`, `SecsDouble8`, `SecsBoolean`, `SecsList`, etc.).
  - **Message model**:
    - `SecsMessage` containing:
      - Stream, Function, W-bit, System Bytes, Device ID, etc.
      - Root `SecsItem` (which may contain nested structures).
  - **Codec**:
    - Encode/decode between `SecsMessage` and raw byte arrays according to E5.
    - Validate lengths, formats, and nested structures.
  - **Optional SML support**:
    - Parser/formatter for SECS Message Language (SML) to assist debugging and testing.

- **Layer 3 – HSMS + SECS Session API**
  - Correlation between **primary** and **secondary** messages using System Bytes and W-bit.
  - Handling of SECS/GEM timers (e.g., T3 for reply timeout) at the message level.
  - High-level API:
    - `Task<SecsMessage> SendAndWaitAsync(SecsMessage primary, TimeSpan timeout)`.
    - Events/callbacks for unsolicited messages.
  - Abstract away raw sockets and byte arrays from the rest of the library.

- **Layer 4 – GEM Core (E30)**
  - **State models**:
    - **Communication State Model**.
    - **Control State Model** (Offline/Online, Local/Remote).
    - (Future) Spooling state model.
  - **Static and dynamic data**:
    - Equipment constants, status variables (SVIDs), equipment constants (ECs), data variables (DVs), etc.
  - **Alarms**:
    - Alarm definitions (ALID, text, severity).
    - Alarm set/clear mechanisms and corresponding S5F1/S5F2, S5F3/S5F4 messages.
  - **Events & data collection**:
    - CEID (Collection Event ID) definitions.
    - Reports (RPTID, list of VIDs).
    - Linking/unlinking of events to reports.
    - Enabling/disabling data collection.
    - S6F11 event reports, S6F1/S6F2 data collection reports.
  - **Remote commands**:
    - Remote command definitions (RCMD + parameter metadata).
    - Handling S2F41 remote command requests and S2F42 replies.
  - Role-specific facades:
    - `IGemEquipment` – interface for equipment-side integration.
    - `IGemHost` – interface for host application integration.

- **Layer 5 – Configuration & Metadata**
  - Descriptive model for GEM configuration:
    - SVIDs, CEIDs, RPTIDs, ALIDs, RCMDs, etc.
  - Backed by JSON/XML/YAML so both equipment and host can load the same configuration.
  - Allow vendor-specific extensions without modifying core code.

- **Layer 6 – Tooling, Samples & Diagnostics**
  - Sample console apps:
    - `SampleEquipment` – equipment simulator.
    - `SampleHost` – host test client.
  - Logging hooks:
    - SECS-II message logs.
    - HSMS connection diagnostics.
    - State transitions for GEM models.
  - (Future) UI tools:
    - Simple viewers for logs and online state inspection.

---

### 4. Phased Implementation Plan

#### Phase 0 – Standards Study & Project Scaffolding

**Objectives**
- Build a strong understanding of key standards:
  - **E5 (SECS-II)**: message structure, data item formats, encoding rules.
  - **E37 (HSMS)**: connection procedures, state machine, control messages, timers.
  - **E30 (GEM)**: concepts, required services, state models, alarms, events, reports, remote commands.
- Set up the solution structure and basic tooling.

**Tasks**
- Create solution structure:
  - `Secsgem.Core` (transport + SECS-II + session).
  - `Secsgem.Gem` (GEM layer).
  - `Secsgem.Samples` (sample host and equipment apps).
  - `Secsgem.Tests` (unit and integration tests).
- Choose:
  - Test framework (e.g. xUnit, NUnit, MSTest).
  - Logging abstraction (custom interfaces, or integration with existing frameworks).
- Write personal summaries / cheat sheets for:
  - E5 encoding rules and data types.
  - E37 state machine and timers.
  - E30 state models and service categories.

**Deliverable**
- Empty but well-structured solution with projects, references, and basic README pointing to this plan.

---

#### Phase 1 – HSMS Transport (E37) & Minimal SECS-II

**Objectives**
- Establish robust TCP-based HSMS connectivity.
- Send and receive minimal SECS-II messages end-to-end.

**Tasks**
- Implement HSMS connection:
  - Active (client/host) and passive (server/equipment) roles.
  - HSMS control messages:
    - SELECT, DESELECT, LINKTEST, SEPARATE.
  - HSMS state machine:
    - NOT CONNECTED → CONNECTED → NOT SELECTED → SELECTED.
  - Timers:
    - Implement T5, T6, T7, T8 with reasonable defaults and configuration.
- Implement basic SECS-II support:
  - Header assembly/parsing (stream, function, W-bit, device ID, system bytes).
  - Minimal `SecsItem` subset (e.g. `List`, `Ascii`, small integer type).
  - A basic send/receive loop on top of HSMS.

**Deliverable**
- Console sample:
  - Equipment app listens for HSMS connections and performs SELECT.
  - Host app connects, sends `S1F1` (Are You There?), equipment replies with `S1F2`.

---

#### Phase 2 – Full SECS-II Data Model & API

**Objectives**
- Provide a complete and robust SECS-II implementation independent of GEM.
- Make it easy to build and inspect messages programmatically and (optionally) via SML.

**Tasks**
- Implement full SECS-II data model:
  - All standard data types supported by E5:
    - Binary, Boolean, all integer sizes (signed/unsigned), floats/doubles, ASCII, JIS, lists, arrays.
  - Validation and boundary checks for lengths and values.
- Implement `SecsMessage` API:
  - Convenient constructors and/or builder patterns.
  - Utilities to clone, compare, and inspect messages.
- (Optional but valuable) Implement SML support:
  - SML parser: text → `SecsMessage`.
  - SML formatter: `SecsMessage` → text.
- Testing:
  - Round-trip tests for each data type:
    - Item → encode → decode → same item.
  - Header encode/decode tests.
  - Representative sample messages from the SEMI spec encoded and decoded.

**Deliverable**
- Stable SECS-II library layer with comprehensive tests and example usage.

---

#### Phase 3 – GEM Core (E30) – Equipment Side MVP

**Objectives**
- Implement the minimal set of GEM features for an equipment-side implementation.
- Make it possible to simulate a realistic GEM-capable piece of equipment.

**Tasks**
- Implement state models:
  - Communication State Model (Connecting, Connected, Selected, etc.).
  - Control State Model (Offline, Local, Remote).
  - Provide events for state changes and public methods/commands to request transitions.
- Alarms:
  - Define `AlarmDefinition` (ALID, severity, description, etc.).
  - API for raising and clearing alarms.
  - Map alarm actions to S5F1/S5F2 (set/clear) and S5F3/S5F4 (list requests).
- Events & reports:
  - Define `EventDefinition` (CEID, description).
  - Define `VariableDefinition` for SVIDs/VIDs (type, units, access mode).
  - Implement reports (RPTID → list of VIDs).
  - Implement linking of events to reports and enable/disable behavior.
  - Implement S6F11 event reports and S6F1/S6F2 data collection reports.
- Remote commands:
  - Define remote commands (RCMD + parameter metadata).
  - Handle S2F41 remote command requests and S2F42 replies.
  - Provide callbacks for application code to implement actual behavior.
- Equipment-side facade:
  - `GemEquipment` (or `GemEquipmentSession`) class that:
    - Wraps HSMS + SECS-II.
    - Hosts state models, alarms, events, reports, and remote commands.
    - Exposes configuration-driven setup of IDs and definitions.

**Deliverable**
- Example equipment simulator:
  - Periodically changing variables (e.g., temperature, pressure).
  - Alarms that can be triggered and cleared.
  - Events that generate S6F11 messages.
  - Remote commands that affect internal state.

---

#### Future Phases (After Equipment Library is Complete)

> The following phases describe **future work** that will be started only after the
> equipment-side library (through Phase 3) is complete and stable. They are **not**
> part of the current implementation scope, but are kept here as roadmap.

##### Phase 4 – Host-Side GEM & Configuration Model (Future)

**Objectives**
- Provide a convenient API for host applications to interact with GEM-capable equipment.
- Introduce a configuration model so both host and equipment share common metadata.

**Tasks**
- Host-side GEM:
  - `GemHost` class that:
    - Manages connections to one or more equipment instances.
    - Provides high-level methods for:
      - Requesting online/remote states.
      - Getting/setting variables (SVIDs/ECs).
      - Subscribing to events and reports.
      - Sending remote commands with typed parameters.
  - Map high-level methods to underlying SxFy SECS messages.
- Configuration & metadata:
  - Define a configuration schema (JSON/XML/YAML) representing:
    - SVIDs, CEIDs, ALIDs, RPTIDs, RCMDs.
    - Data types, ranges, units where applicable.
  - Implement loaders that:
    - Instantiate `GemEquipment` and `GemHost` behavior from configuration.
  - Allow vendor-specific extensions via custom fields or plug-in modules.

**Deliverable**
- Sample host application that:
  - Loads configuration.
  - Connects to the equipment simulator.
  - Puts equipment Online/Remote.
  - Subscribes to events and prints S6F11 messages.
  - Sends remote commands (e.g., start/stop, recipe select) and displays results.

---

##### Phase 5 – Advanced GEM & GEM300 (Long-Term, Future)

**Objectives**
- Extend beyond base GEM to support advanced GEM300-related functionality.

**Tasks (high-level)**
- Implement selected GEM300 features:
  - Control Jobs (CJ) and Process Jobs (PJ).
  - Lot/cassette/wafer handling as appropriate for target use cases.
  - Support for related standards where needed (E39, E87, E90, E94, etc.).
- Extend configuration schema to include:
  - Job definitions, routes, recipe-related metadata.
- Update samples:
  - Demonstrate a simple GEM300-like flow if possible (even if simplified).

**Deliverable**
- Extended library that can be used as a starting point for GEM300-style equipment/host integration.

---

### 5. Coding Style & Design Guidelines

- **Block-structured methods**:
  - Avoid excessive early returns where possible.
  - Aim for clear, readable control flow with well-defined sections for:
    - Validation.
    - Main logic.
    - Cleanup/error handling (or use exceptions where appropriate).
- **Separation of concerns**:
  - Transport layer knows nothing about GEM or equipment logic.
  - GEM layer uses SECS-II and transport through interfaces, not concrete implementations.
- **Testability**:
  - Use interfaces for key abstractions (e.g., `IHsmsConnection`, `ISecsCodec`, `IGemEquipmentSession`).
  - Provide mocks/fakes in unit tests to validate behavior without network I/O.
- **Extensibility**:
  - Allow custom streams/functions and vendor-specific variables without changing the framework core.
  - Use configuration where possible instead of hardcoding IDs in logic.

---

### 6. Initial Milestones Checklist (Equipment-Focused)

- **Milestone 1 (1–2 weeks)**
  - [ ] Create solution and projects (`Core`, `Gem`, `Samples`, `Tests`).
  - [ ] Implement skeletons for `SecsItem`, `SecsMessage`, `IHsmsConnection`.
  - [ ] Implement basic HSMS connect/disconnect and LINKTEST.
  - [ ] Simple demo: send a dummy SECS-like message object (even before full encoding).

- **Milestone 2 (2–4 weeks)**
  - [ ] Implement full SECS-II header and body encoding/decoding.
  - [ ] Implement enough `SecsItem` types for S1F1/S1F2.
  - [ ] Demo: `S1F1`/`S1F2` exchange between host and equipment console apps.

- **Milestone 3 (4–8 weeks)**
  - [ ] Implement HSMS state machine and timers fully (E37).
  - [ ] Implement basic GEM Communication + Control state models.
  - [ ] Demo: Equipment goes Online/Remote; host requests and observes state.

- **Milestone 4 (2–3 months)**
  - [ ] Implement alarms, events, reports, and remote commands (equipment side).
  - [ ] Demo: Equipment simulator with changing variables, event-triggered S6F11, and remote commands.

- **Future Milestone (after equipment library is complete)**
  - [ ] Implement host-side GEM convenience API.
  - [ ] Implement configuration model for SVID/CEID/etc.
  - [ ] Improve logging, documentation, and tests (host + shared config).

---

### 7. Next Steps for Documentation

You can now:

- Add **references** to this file:
  - Links to SEMI specs (E5, E30, E37, others you own).
  - Your own notes and cheat sheets on each standard.
- Create additional docs:
  - `docs/E5-summary.md` – your notes on SECS-II.
  - `docs/E30-summary.md` – your notes on GEM.
  - `docs/E37-summary.md` – your notes on HSMS.

After documentation is in place, the next practical step is to design the initial **C# interfaces and classes** (signatures only) for:
- HSMS connection abstraction.
- SECS-II message and item hierarchy.
- Basic GEM equipment session interface.

