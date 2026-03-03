## OSI Model Mapping for Secsgem

This document defines how the **Secsgem** library aligns with the **OSI model** and the
relevant **SEMI standards**. All detailed designs and implementations should stay
consistent with this mapping.

### Overview Table

| OSI Layer | OSI Responsibility | SEMI Standard | Secsgem Responsibility |
|----------:|--------------------|--------------|------------------------|
| 7 Application | High-level APIs and application semantics | **E30 GEM** | `Secsgem.Gem` (equipment-side GEM services, state models, variables, collection events, reports, alarms, remote commands) |
| 6 Presentation | Data representation and encoding | **E5 SECS-II** | `Secsgem.Core.Secs` (data item types, message structure, encode/decode) |
| 5 Session | Managing communication sessions and conversations | **E37 HSMS** | `Secsgem.Core.Session` + `Secsgem.Core.Transport` (HSMS connection management, SECS message sessions, timers, transactions) |
| 4 Transport | Reliable transport between endpoints | TCP/IP | .NET sockets / OS networking (used by `IHsmsConnection` implementation) |
| 3 Network | Routing, addressing | IP | Outside of Secsgem scope (provided by OS/network) |
| 2 Data Link | Frames on a physical link | Ethernet (etc.) | Outside of Secsgem scope |
| 1 Physical | Electrical/optical signaling | Physical medium | Outside of Secsgem scope |

### Secsgem Library Alignment

#### Layer 5–6: SECS-II and HSMS (`Secsgem.Core`)

- **`Secsgem.Core.Secs` (Presentation / E5)**:
  - Represents **SECS-II data items** (`SecsItem` hierarchy: list, binary, integers, floats, booleans, ASCII/JIS).
  - Represents **message structure** (`SecsHeader`, `SecsMessage`).
  - Implements **encoding/decoding** between `SecsMessage` and raw byte arrays (`ISecsCodec`, `SecsCodec`).
  - Responsible only for *content and structure* of messages, not transport.

- **`Secsgem.Core.Transport` + `Secsgem.Core.Session` (Session / E37)**:
  - `IHsmsConnection` / `HsmsConnection`:
    - Implements **HSMS** over TCP/IP: connection establishment, linktest, separation, timers (T5–T8).
  - `ISecsSession` / `SecsSession`:
    - Manages **SECS message sessions**:
      - Correlates primary and secondary messages using **System Bytes** and W-bit.
      - Tracks **open transactions** and **timeouts** (transaction and conversation protocols from E5).
      - Provides high-level APIs like `SendAndWaitAsync`.
  - These layers must not know anything about GEM concepts (state models, alarms, etc.).

#### Layer 7: GEM (`Secsgem.Gem`)

- **`Secsgem.Gem` (Application / E30, equipment-side only for current scope)**:
  - Implements **GEM services and state models** as defined in E30:
    - Communication State Model.
    - Control State Model.
    - (Future) Spooling state model.
  - Manages **static and dynamic data**:
    - Equipment constants (ECs).
    - Status variables (SVIDs / SVs).
    - Data variables (DVs).
  - Implements **data collection**:
    - Collection events (CEIDs).
    - Reports (RPTIDs, lists of variables).
    - Trace data collection.
  - Implements **alarms**:
    - Alarm definitions (ALIDs, severity).
    - Alarm set/clear behavior and associated S5/S6 messages.
  - Implements **remote commands**:
    - Remote command definitions (RCMD + parameters).
    - Handling S2F41/S2F42 requests and replies.
  - Uses `Secsgem.Core.Session` and `Secsgem.Core.Secs` underneath, but exposes a higher-level, equipment-oriented API (`GemEquipmentSession`, `IGemEquipment`).

### Design Rules Based on This Mapping

- **GEM must not bypass SECS-II/HSMS**:
  - All communication between host and equipment goes through **GEM → SECS-II → HSMS → TCP/IP**.
  - No direct socket access from GEM layer; it uses `ISecsSession` and `IHsmsConnection` abstractions.

- **No GEM logic in `Secsgem.Core`**:
  - Core layers are **generic SECS/HSMS**; they must be usable without GEM.
  - This keeps the library reusable for non-GEM SECS/HSMS use cases.

- **Equipment-side only (current scope)**:
  - All **application-layer APIs** in the first version are focused on **equipment behavior** (E30 equipment role).
  - Host-side convenience APIs will be added in a later phase, but must still respect the same OSI mapping.

By keeping future class and module designs consistent with this document, we ensure the implementation stays aligned with both the OSI model and the SEMI standards (E5, E30, E37).

