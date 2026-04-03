# RegOS — Regulatory Operating System

**The end-to-end regulatory technology platform for financial supervision, compliance reporting, and institutional governance.**

RegOS is a multi-tenant SaaS platform that connects financial regulators (central banks, deposit insurance corporations) with the institutions they supervise. It digitises the entire regulatory returns lifecycle — from template design and data collection through validation, submission, approval, and intelligence — replacing manual spreadsheet workflows with an auditable, real-time system.

The platform ships as three deployable artefacts: an **Admin Portal** (regulator/platform operator UI), an **Institution Portal** (supervised entity UI), and a **REST API** (programmatic access & CaaS).

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Multi-Tenancy & Access Control](#2-multi-tenancy--access-control)
3. [Template & Schema Management](#3-template--schema-management)
4. [Submission & Data Collection](#4-submission--data-collection)
5. [Validation Engine](#5-validation-engine)
6. [Maker-Checker Approval Workflow](#6-maker-checker-approval-workflow)
7. [Filing Calendar & Deadline Management](#7-filing-calendar--deadline-management)
8. [Direct Regulatory Submission](#8-direct-regulatory-submission)
9. [Compliance Dashboards & Analytics](#9-compliance-dashboards--analytics)
10. [Predictive Intelligence (ForeSight)](#10-predictive-intelligence-foresight)
11. [Anomaly Detection](#11-anomaly-detection)
12. [Early Warning System](#12-early-warning-system)
13. [CAMELS Scoring & Systemic Risk](#13-camels-scoring--systemic-risk)
14. [Stress Testing & Policy Simulation](#14-stress-testing--policy-simulation)
15. [Conduct Risk & Sanctions Screening](#15-conduct-risk--sanctions-screening)
16. [RegulatorIQ — Conversational Intelligence](#16-regulatoriq--conversational-intelligence)
17. [Cross-Border & Consolidated Supervision](#17-cross-border--consolidated-supervision)
18. [Capital Planning & Optimisation](#18-capital-planning--optimisation)
19. [Examination Workspace](#19-examination-workspace)
20. [Export & Reporting](#20-export--reporting)
21. [Notifications & Communication](#21-notifications--communication)
22. [Subscription & Billing](#22-subscription--billing)
23. [Onboarding & Help](#23-onboarding--help)
24. [Partner & White-Label](#24-partner--white-label)
25. [Data Protection & Privacy](#25-data-protection--privacy)
26. [Compliance-as-a-Service (CaaS) API](#26-compliance-as-a-service-caas-api)
27. [Platform Intelligence Workspace](#27-platform-intelligence-workspace)
28. [Design System & Accessibility](#28-design-system--accessibility)
29. [Security & Infrastructure](#29-security--infrastructure)
30. [Technology Stack](#30-technology-stack)

---

## 1. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        RegOS Platform                            │
│                                                                  │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────────────┐ │
│  │  Admin Portal │   │ Institution  │   │     REST API         │ │
│  │  (Blazor SSR) │   │   Portal     │   │  (ASP.NET Minimal)   │ │
│  │  Regulator UI │   │ (Blazor SSR) │   │  CaaS / Webhooks     │ │
│  └──────┬───────┘   └──────┬───────┘   └──────────┬───────────┘ │
│         │                  │                       │             │
│  ┌──────┴──────────────────┴───────────────────────┴───────────┐ │
│  │                   Application Services                       │ │
│  │  Validation · Ingestion · Filing · Notifications · Export    │ │
│  └──────────────────────────┬──────────────────────────────────┘ │
│                             │                                    │
│  ┌──────────────────────────┴──────────────────────────────────┐ │
│  │                      Domain Model                            │ │
│  │  108 Entities · Enums · Value Objects · Abstractions         │ │
│  └──────────────────────────┬──────────────────────────────────┘ │
│                             │                                    │
│  ┌──────────────────────────┴──────────────────────────────────┐ │
│  │                    Infrastructure                            │ │
│  │  SQL Server (RLS) · Dapper · EF Core · Serilog · JWT        │ │
│  │  Anthropic LLM · SMTP · SMS · Webhooks · Blob Storage       │ │
│  └─────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

**Layer separation** follows Clean Architecture: Domain (entities, enums, abstractions) → Application (orchestrators, DTOs, service interfaces) → Infrastructure (persistence, integrations, 130+ service implementations) → Presentation (Blazor UI, API endpoints).

---

## 2. Multi-Tenancy & Access Control

### Tenant Hierarchy

RegOS supports a hierarchical tenant model:

| Level | Description | Example |
|-------|-------------|---------|
| **Platform** | The RegOS operator | Central Bank deployment |
| **Tenant** | A regulated jurisdiction or regulator | CBN, NDIC |
| **Institution** | A supervised financial entity | First Bank, GTBank |
| **Entity** | Organisational unit within an institution | Head Office, Branch |

Tenants progress through lifecycle states: `PendingActivation → Active → Suspended → Deactivated → Archived`.

### Row-Level Security

Every major table carries a `TenantId` foreign key. SQL Server `SESSION_CONTEXT` is set per-request via middleware, enforcing row-level security at the database layer — no tenant can ever read another tenant's data, regardless of application bugs.

### Role-Based Access

**Admin Portal Roles:**
- **Platform Admin** — full system access, tenant management, impersonation
- **Approver** — template and configuration review
- **Viewer** — read-only dashboards and reports

**Institution Portal Roles:**
- **Institution Admin** — tenant settings, team management, branding
- **Maker** — create and submit returns
- **Checker** — approve/reject submissions (maker-checker workflow)
- **Viewer** — read-only submission and dashboard access
- **Approver** — elevated approval privileges

Fine-grained permissions are assigned per role, with per-user overrides stored as JSON arrays. API keys carry their own permission sets with rate limits.

### Authentication

- **Email/Password** with configurable password policy (12+ characters minimum)
- **Multi-Factor Authentication** — TOTP (authenticator apps) with SMS fallback and backup codes
- **SAML 2.0 SSO** — per-tenant IdP configuration with JIT provisioning
- **Biometric** — Face ID / Touch ID / Windows Hello detection
- **JWT** — RSA-signed access and refresh tokens for API authentication
- **Session Management** — inactivity warning, countdown modal, cross-tab sync via BroadcastChannel, circuit disconnect detection

---

## 3. Template & Schema Management

Templates define the structure of regulatory returns. Each template represents a form that institutions must fill and submit.

### Template Lifecycle

```
Draft → Review → Published → Deprecated → Retired
```

### Structural Categories

| Category | Description | Use Case |
|----------|-------------|----------|
| **FixedRow** | Predefined rows, institutions fill values | Balance sheet items |
| **MultiRow** | Institutions add variable number of rows | Loan schedules |
| **ItemCoded** | Each row identified by a code from a master list | Chart of accounts |

### Template Features

- **Versioning** — each template carries numbered versions; institutions submit against a specific version
- **Field Definitions** — field code, label, data type (Money, Integer, Decimal, Text, Date, Boolean, Percentage), validation constraints
- **Intra-Sheet Formulas** — Sum, Difference, Equals, GreaterThan, LessThan, Between, Ratio, Custom, Required — with tolerance (amount and percentage)
- **Cross-Sheet Rules** — validation rules spanning multiple templates/modules with aggregate functions (SUM, COUNT, MAX, MIN, AVG)
- **Business Rules** — DateCheck, ThresholdCheck, Completeness, Custom expression-based rules
- **XSD Generation** — automatic XML schema generation for each template version
- **Module Assignment** — templates belong to regulatory modules (e.g., MFCR, QFCR, SFCR)
- **Frequency** — Monthly, Quarterly, Semi-Annual, or Computed

### Admin Portal — Template Management

The Admin Portal provides a full template designer:
- Template list with search, filtering, and status indicators
- Version management with field CRUD operations
- Formula builder for intra-sheet validation
- Cross-sheet rule configuration
- Auto-save with debounce (3-second delay), navigation guard, and retry on failure
- Slug auto-generation from template names
- Template detail view with metadata strip

---

## 4. Submission & Data Collection

### Submission Channels

Institutions can submit data through four channels:

1. **XML Upload** — upload structured XML files conforming to the template's XSD
2. **Online Form** — browser-based data entry with real-time validation
3. **Bulk Upload** — batch XML file submission for multiple returns
4. **API** — programmatic submission via the REST API or CaaS endpoint

### Online Data Entry Form

The form-based submission provides a rich editing experience:

- **Progress sidebar** — sticky right panel showing completion percentage (SVG ring), section-by-section status, required field counter, and "Jump to next empty field" button
- **Section navigator** — collapsible left sidebar with section status icons (✓ complete, ⚠ warning, ✗ error, ○ empty)
- **Sticky submit bar** — appears when completion ≥ 50%, fixed to bottom-right
- **Mobile responsive** — sidebar collapses to bottom drawer on screens ≤ 900px
- **Auto-population** — inter-module data flow automatically populates fields from prior submissions
- **Field-level change tracking** — every edit is recorded with before/after values and user attribution

### Submission Status Flow

```
Draft → Parsing → Validating → Accepted / AcceptedWithWarnings / Rejected
                                    ↓
                             PendingApproval → Approved / ApprovalRejected
                                    ↓
                          SubmittedToRegulator → RegulatorAcknowledged
                                    ↓
                          RegulatorAccepted / RegulatorQueriesRaised
```

### Batch Submissions

- Create batches grouping multiple submissions
- Track batch status with progress indicators
- Handle regulatory queries per batch
- Retry mechanism with max attempt tracking
- Correlation ID for end-to-end tracing

### Supporting Features

- **Carry Forward** — populate new-period submissions from the prior period's data
- **Draft Management** — save work-in-progress without triggering validation
- **Data Source Tracking** — each field value tagged as Manual, Import, Computed, or System
- **Submission Timeline** — visual audit trail of every status change with timestamps

---

## 5. Validation Engine

RegOS implements a five-phase validation pipeline that runs on every submission:

### Phase 1: Schema Validation
Validates the submitted XML/data against the template's structural definition — correct fields, data types, required values.

### Phase 2: Type & Range Validation
Checks that each value falls within its declared type constraints — numeric ranges, date formats, string lengths, percentage bounds.

### Phase 3: Intra-Sheet Validation
Evaluates formulas defined within a single template:
- Sum checks (e.g., row totals must equal column total)
- Difference constraints
- Ratio calculations with tolerance
- Custom expressions
- Tolerance support (absolute amount and percentage)

### Phase 4: Cross-Sheet Validation
Applies rules spanning multiple templates within the same module:
- Aggregate functions across templates (SUM, COUNT, MAX, MIN, AVG)
- Cross-template equality checks
- Severity-based handling (Error blocks submission; Warning allows with flag)

### Phase 5: Business Rule Validation
Runs configurable business rules:
- Date consistency checks
- Threshold monitoring
- Completeness validation
- Custom expression evaluation
- Tenant-specific or global rule application

### Validation Output

Each validation produces a `ValidationReport` containing individual `ValidationError` records with:
- Category and severity (Info / Warning / Error)
- Rule reference and field location
- Expected vs. actual values
- Cross-return code references for cross-sheet issues

### Dry-Run Validation

Institutions can preview validation results before formal submission, enabling iterative correction without polluting the audit trail.

---

## 6. Maker-Checker Approval Workflow

For institutions that require dual control, RegOS enforces a maker-checker workflow:

1. **Maker** prepares and submits a return
2. Submission enters `PendingApproval` status
3. **Checker** reviews the submission with full validation report
4. Checker **approves** (advances to `SubmittedToRegulator`) or **rejects** (with comments; Maker can revise and resubmit)

### Approval Features

- Configurable per-institution (toggle in Institution Settings)
- Approval audit trail with requester, reviewer, timestamps, and notes
- Re-submission tracking linking revised submissions to originals
- Batch approval for multiple submissions
- Live presence awareness (see who else is viewing the approval queue)

---

## 7. Filing Calendar & Deadline Management

### Return Periods

Each module generates return periods based on its frequency:
- **Monthly** — 12 periods per year
- **Quarterly** — 4 periods per year
- **Semi-Annual** — 2 periods per year
- **Computed** — custom cadence

### Period Lifecycle

```
Upcoming → Open → DueSoon → Overdue → Completed / Closed
```

### Deadline Management

- **Default deadlines** — derived from module frequency and deadline offset
- **Deadline overrides** — regulators can extend deadlines with documented reasons
- **Escalation levels** — automated notifications at T-30, T-14, T-7, T-3, T-1, and T+0 days
- **Auto-draft creation** — system creates draft submissions at T-60 to prompt early preparation

### RAG Status

Every period carries a Red/Amber/Green status:
- **Green** — submitted on time or ahead of deadline
- **Amber** — due soon, not yet submitted
- **Red** — overdue

### SLA Tracking

`FilingSlaRecord` tracks timeliness per submission:
- On-time, early, or late classification
- Days-to-deadline calculation
- Module × Period × Submission mapping

### Calendar UI

The portal provides a visual calendar view with:
- Monthly/quarterly deadline display
- Colour-coded submission status
- Deadline countdown indicators

---

## 8. Direct Regulatory Submission

RegOS can submit returns directly to regulator APIs (e.g., CBN, NDIC):

### Submission Pipeline

```
Package Creation → Digital Signature → API Submission → Status Polling → Evidence Package
```

### Digital Signatures

- **RSA-SHA512** and **ECDSA-SHA384** signature algorithms
- Certificate thumbprint verification
- RFC 3161 timestamp tokens
- Signed data hash for tamper detection

### Evidence Packages

After successful submission, RegOS generates an immutable evidence package:
- ZIP archive containing all submission artefacts
- SHA-256 hash verification
- Timestamped with user attribution
- Stored with file size tracking

### Resilience

- Automatic retry mechanism (up to 3 attempts)
- Error tracking with detailed failure reasons
- Status progression: `Pending → Packaging → Signed → Submitted → Acknowledged`

### Certificates

Institutions receive downloadable compliance certificates for successful submissions. Third parties can verify certificate authenticity via a public verification URL (`/verify/{CertId}`).

---

## 9. Compliance Dashboards & Analytics

### Institution Portal Dashboards

**Home Dashboard** — command centre showing:
- Portal posture banner with compliance status
- Deadline pressure tracking (overdue, submitted, due soon)
- Module workspace summary with pending counts
- Drag-and-drop customisable widget layout
- Alert banners for overdue items

**Admin Dashboard** — tenant-wide metrics:
- Usage tiles (subscriptions, submissions, users, data)
- User activity tracking (top contributors, login metrics)
- Billing status and outstanding balances
- Notification delivery statistics
- Data quality trends

**Compliance Dashboard** — detailed compliance view:
- Compliance pulse banner with quarter-over-quarter changes
- Module-by-module compliance rates
- Monthly submission bar charts (on-time vs. missed)
- Filter by return code and frequency
- PDF export and email delivery

**Module Dashboard** — per-module deep dive:
- Module-specific compliance and submission details
- Return-level status tracking

**Compliance Health Dashboard** — institutional health indicators:
- Multi-pillar scoring (Filing Timeliness 25%, Data Quality 25%, Regulatory Capital 20%, Governance 15%, Engagement 15%)
- 12-month rolling trend analysis
- Peer comparison
- Alert generation for score degradation

**Consolidation Dashboard** — cross-institution or consolidated reporting views

### Admin Portal Dashboards

**Platform Dashboard** — system-wide operational view:
- Tenant health overview
- Submission volume metrics
- System performance indicators

**Sector Analytics** — aggregate sector insights:
- CAR distribution analysis
- NPL trend tracking
- Deposit structure analysis
- Cross-sector performance comparison

---

## 10. Predictive Intelligence (ForeSight)

ForeSight is RegOS's predictive analytics engine, running as a daily background computation job.

### Capabilities

| Prediction | Description |
|------------|-------------|
| **Filing Risk** | Probability of late submissions per institution, with feature importance analysis |
| **Capital Breach Forecast** | Regulatory capital adequacy predictions with confidence intervals |
| **Compliance Trend** | Institution compliance trajectory modelling |

### Features

- Risk band classification: Critical / High / Medium / Low
- Confidence intervals (lower/upper bounds)
- Feature importance analysis identifying key risk drivers
- Root cause narratives explaining predictions
- Actionable recommendations
- Daily automated computation via `ForeSightDailyComputationJob`

### Portal UI

- **ForeSight Dashboard** (`/analytics/foresight`) — overview of all predictions
- **Filing Risks** (`/analytics/foresight/filing-risks`) — detailed risk scoring per institution
- **Compliance Trend** (`/analytics/foresight/compliance-trend`) — trend projections
- **Capital Forecast** (`/analytics/foresight/capital-forecast`) — capital pressure analysis

---

## 11. Anomaly Detection

### Detection Engine

The `AnomalyDetectionService` identifies statistical outliers in submitted data:

- **Rule-based detection** with configurable baselines
- **Peer group analysis** — statistical comparison against similar institutions
- **Field-level detection** — individual value anomalies
- **Correlation-based detection** — relationship anomalies between fields

### Model Training

`AnomalyModelTrainingService` provides an ML training pipeline:
- Bootstrap model generation from historical submission data
- Field model training with R-squared validation
- Correlation rule generation
- Peer group statistics computation

### Portal UI

- **Anomaly Dashboard** (`/analytics/anomalies`) — flagged anomalies and trends
- **Anomaly Detail** (`/analytics/anomalies/{ReportId}`) — drill-down investigation
- PDF export of anomaly reports with visualisations

---

## 12. Early Warning System

### Early Warning Indicators (EWI)

The `EarlyWarningService` monitors key risk metrics:

| Indicator | Threshold Type |
|-----------|---------------|
| Capital Adequacy Ratio (CAR) | Below minimum threshold |
| Non-Performing Loan Ratio (NPL) | Above ceiling |
| Liquidity Coverage Ratio (LCR) | Below requirement |
| Deposit Concentration | Above risk threshold |
| Related Party Lending | Above regulatory limit |

### Features

- Time series analysis for trend detection
- RAG severity classification (Red / Amber / Green)
- Trigger date tracking
- Institutional correlation analysis
- Automated supervisory action recommendations

---

## 13. CAMELS Scoring & Systemic Risk

### CAMELS Framework

The `CAMELSScorer` implements the standard CAMELS rating system:

| Component | Area |
|-----------|------|
| **C** | Capital Adequacy |
| **A** | Asset Quality |
| **M** | Management Quality |
| **E** | Earnings |
| **L** | Liquidity |
| **S** | Sensitivity to Market Risk |

- Institution-level composite scoring
- Rating classification: Green / Amber / Red / Critical
- Historical score tracking

### Systemic Risk

The `SystemicRiskService` aggregates individual institutional risk into sector-wide views:

- **Risk heatmap** — visual risk density across institutions
- **Contagion analysis** — `ContagionCascadeEngine` models systemic spillover effects
- **Systemic risk index** — aggregate sector risk indicator
- **Supervisory action generation** — automated recommendations from risk flags
- **Cross-institutional correlation** — identifies risk clustering

### Benchmarking

- `EntityBenchmarkingService` — peer comparison across key metrics
- `BenchmarkingService` — cross-sector performance analysis
- `SectorAnalyticsService` — CAR distribution, NPL trends, deposit structure

---

## 14. Stress Testing & Policy Simulation

### Stress Testing

The `StressTestService` provides a regulatory stress testing framework:

**Predefined Scenarios:**
- NGFS Orderly Transition
- NGFS Disorderly Transition
- NGFS Hot House World
- Oil Price Collapse
- Custom scenarios

**Shock Parameters:**
- GDP growth
- Foreign exchange rates
- Interest rates
- Credit loss rates

**Output:**
- Entity-level impact calculations
- Capital adequacy impact modelling
- Multi-scenario comparison
- Report generation via `StressTestReportGenerator`

### Policy Simulation

The `PolicyScenarioService` enables regulators to model the impact of regulatory changes before implementation:

- Regulatory parameter change simulation
- Entity compliance gap analysis
- Cost-benefit analysis (`CostBenefitAnalyser`)
- Phase-in scenario modelling
- Impact tracking with accuracy measurement (`HistoricalImpactTrackerService`)

### Admin Portal Pages

- **Policy Simulation Detail** — full scenario modelling workspace
- **Stress Test dashboards** — results visualisation and comparison

---

## 15. Conduct Risk & Sanctions Screening

### Conduct Risk Scoring

The `ConductRiskScorer` computes a multi-factor conduct risk score:

| Factor | Weight |
|--------|--------|
| Market Abuse | 30% |
| AML Effectiveness | 25% |
| Insurance Conduct | 15% |
| Customer Conduct | 15% |
| Governance | 10% |
| Sanctions History | 5% |

### Surveillance Monitors

- `InsuranceConductMonitor` — insurance sector conduct surveillance
- `AMLConductMonitor` — AML/CFT compliance monitoring
- `BDCFXSurveillance` — foreign exchange trading surveillance

### Sanctions Screening

- `ScreeningEngineService` — real-time screening with alert generation
- `SanctionsWatchlistCatalogService` — dynamic watchlist management
- `SanctionsScreeningSessionStoreService` — screening session persistence

### Admin Portal

- **Conduct Surveillance** page — unified conduct risk dashboard

---

## 16. RegulatorIQ — Conversational Intelligence

RegulatorIQ is an AI-powered conversational interface for regulators to query compliance data using natural language.

### Architecture

```
User Query → Intent Classifier → Data Source Orchestration → Response Generator → Cited Answer
```

### Components

- **RegulatorIntentClassifier** — NLP-based intent recognition with LLM fallback (Anthropic Claude)
- **RegulatorIntelligenceService** — multi-turn conversation management with context awareness
- **RegulatorResponseGenerator** — intelligent response generation with citations and follow-up suggestions
- **ComplianceIqService** — natural language compliance querying

### Features

- Multi-turn conversation with full context preservation
- Classification levels for query sensitivity
- Cross-entity data analysis
- Citation-backed responses
- Follow-up suggestion generation
- Conversation history export to PDF with audit trails

### LLM Integration

RegOS integrates with Anthropic Claude via `AnthropicLlmService` (`ILlmService` abstraction) for:
- Intent classification fallback
- Natural language response generation
- Complex query understanding

### Portal UI

- **ComplianceIQ Chat** (`/analytics/chat`) — institution-facing conversational analytics
- **Chat History** (`/analytics/chat/history`) — past conversation archive
- **RegulatorIQ Chat** (Admin) — regulator-facing intelligence panel

---

## 17. Cross-Border & Consolidated Supervision

### Cross-Border Intelligence

For regulators supervising institutions with cross-border operations:

- **CrossBorderDataFlowEngine** — tracks data flows across jurisdictions
- **ConsolidationEngine** — consolidated group supervision calculations
- **DivergenceDetectionService** — identifies divergence across borders
- **PanAfricanDashboardService** — regional supervisory view

### Contagion Modelling

- **ContagionCascadeEngine** — models how failure at one institution cascades through the system
- **SystemicRiskAggregatorService** — computes aggregate risk metrics

### Admin Portal Pages

- **Cross-Border Dashboard** — multi-jurisdiction overview
- **Contagion Network** — visual network graph of systemic interconnections

---

## 18. Capital Planning & Optimisation

### Capital Stack Optimiser

The `CapitalStackOptimizerService` provides RWA optimisation recommendations:

- Risk-weighted asset analysis
- Optimisation strategies: Collateral, Rebalance, Issuance, Dividend
- Scenario persistence via `CapitalPlanningScenarioStoreService`
- Action catalogue with implementation guidance (`CapitalActionCatalogService`)

---

## 19. Examination Workspace

RegOS provides a workspace for on-site and off-site examinations:

### Features

- **Examination Projects** — scoped investigation workspaces
- **Examiner Queries** — question management with response tracking
- **Query Responses** — institution response submission with attachments
- **Evidence Collection** — document gathering with audit trails
- **Examination Briefing Packs** — auto-generated briefing documents:
  - Executive view with strategic summary
  - Governor view with systemic perspective
  - Director/examiner operational dashboards

### Intelligence Briefing

The `DashboardBriefingPackBuilder` generates role-specific intelligence summaries with:
- Auto-generated insights and recommendations
- Risk signal aggregation
- Historical context
- PDF export via `ExaminationBriefingDocument`

### Knowledge Systems

- `KnowledgeGraphCatalogService` — structured knowledge representation
- `KnowledgeGraphDossierCatalogService` — institutional dossier compilation

### Admin Portal Pages

- **Exam Intelligence Pack** — examination workspace
- **Examiner Dashboard** — examination team view
- **Kanban-style exam management** — visual workflow tracking

---

## 20. Export & Reporting

### Export Formats

| Format | Use Case |
|--------|----------|
| **Excel** | Data analysis and manipulation |
| **PDF** | Formal reports and certificates |
| **XML** | Machine-readable regulatory submissions |
| **XBRL** | International regulatory reporting standard |

### Export Pipeline

```
Export Request (Queued) → Processing → SHA-256 Hash → Completed / Failed
```

- Asynchronous queuing for large exports
- SHA-256 hash verification for integrity
- Expiration tracking
- Error logging and retry

### Custom Reports

- **Report Builder** (`/reports/builder`) — drag-and-drop report designer with:
  - Field selection and ordering
  - Condition builder for filtering
  - Real-time result preview
  - Save and share report configurations
  - Enterprise plan feature

- **Saved Reports** — library of configured report definitions

### Pre-Built Reports

- **Compliance Report** — compliance metrics and trends
- **Audit Trail Report** — user action tracking and compliance audit
- **Board Pack** — executive-level regulatory reporting package
- **Compliance Certificate** — per-submission regulatory certificate

---

## 21. Notifications & Communication

### Channels

| Channel | Description |
|---------|-------------|
| **In-App** | Real-time portal notifications |
| **Email** | SMTP-based with HTML templates |
| **SMS** | Provider-based (Twilio) with quiet hours |
| **Push** | Browser/device push notifications |

### Notification Features

- **Multi-channel delivery** — events route to configured channels per user preference
- **Template system** — `EmailTemplate` entities with HTML/plain text, tenant-specific overrides, and variable interpolation
- **Retry mechanism** — failed deliveries retry with configurable max attempts
- **Delivery tracking** — status progression: `Queued → Sent → Delivered → Failed → Bounced`
- **User preferences** — per-event-type channel toggles with SMS quiet hours (WAT timezone)
- **Priority levels** — Low / Normal / High / Critical

### Notification UI

The portal provides a rich notification centre:
- Filter tabs: All, Submissions, Approvals, Deadlines, System, Audit
- Keyboard shortcuts for tab switching
- Mark all as read
- Context menu: snooze (1 hour, tomorrow), mute type
- Unread count badges throughout the UI

### Webhooks

For external system integration:
- Tenant-scoped webhook endpoints
- Event type filtering
- HMAC signature verification
- Failure tracking with auto-disable
- Delivery history

---

## 22. Subscription & Billing

### Plan Tiers

RegOS supports tiered subscription plans with configurable limits:

| Limit | Description |
|-------|-------------|
| Max Modules | Number of regulatory modules |
| Users per Entity | Team members per institution |
| Max Entities | Organisational units |
| API Calls / Month | Rate-limited API access |
| Storage | Document and data storage |

### Subscription Lifecycle

```
Trial → Active → PastDue → Suspended → Cancelled / Expired
```

- Trial periods (14 days default)
- Monthly and annual billing frequencies
- Grace periods before suspension

### Billing Features

- **Plan Management** — hero display with renewal countdown, comparison table, inline upgrade modal (2-step: plan selection → checkout)
- **Module Pricing** — per-module pricing with base-plan inclusion tracking
- **Invoice Generation** — line items, VAT calculation (7.5% default), period tracking
- **Payment Processing** — status tracking (Pending / Confirmed / Failed / Cancelled)
- **Usage Tracking** — daily `UsageRecord` capturing active users, entities, modules, submissions, storage, and API calls
- **Overdue Banner** — subscription overdue alert displayed across all billing pages

### Portal Pages

- **My Plan** (`/subscription/my-plan`) — plan details, usage meters, upgrade flow
- **Modules** (`/subscription/modules`) — module activation with spring toggle and confirmation dialog
- **Invoices** (`/subscription/invoices`) — card grid with expandable timeline, HTML invoice download
- **Payment History** (`/subscription/payments`) — payment timeline with status dots

---

## 23. Onboarding & Help

### Onboarding Flow

**Wizard** (`/onboarding/wizard`) — 5-step registration:
1. **Profile** — institution name, contact information
2. **Plan Selection** — tier comparison and selection
3. **User Provisioning** — initial user setup
4. **Module Selection** — regulatory module entitlements
5. **Team Invitation** — invite team members
6. **Completion** — confetti celebration animation

**Post-Signup:**
- **Onboarding Checklist** — guided task list for initial configuration
- **Role-Specific Tour** — interactive feature walkthrough based on user role
- **Sandbox Environment** — safe space for learning without affecting production data

### Help System

- **Help Centre** (`/help`) — central resource hub with topic directory and search
- **Getting Started Guide** — quick-start walkthrough
- **Submission Guide** — XML structure, online form, and maker-checker instructions
- **Knowledge Base** — searchable article library
- **FAQ** — frequently asked questions
- **Validation Errors Reference** — error code lookup with resolution guidance
- **Contact Support** — ticket creation and escalation

### In-App Assistance

- **Contextual Help Drawer** — slide-out help panel triggered from any page
- **Contextual Tips** — inline hints for complex features
- **Guided Tours** — multi-step product walkthroughs with beacon highlights
- **Help Rating** — article usefulness feedback widget
- **Onboarding FAB** — floating action button for onboarding prompts

---

## 24. Partner & White-Label

RegOS supports a partner model where service providers manage multiple institutions:

### Partner Dashboard

- Client health heatmap across the portfolio
- Churn pressure indicators
- White-label operating posture metrics
- 6-hero statistic overview
- Client portfolio summary

### Partner Features

- **Sub-Tenant Management** — create and manage white-label customer tenants
- **Sub-Tenant Detail** — per-tenant dashboard and metrics
- **Support Escalation** — escalation queue for sub-tenant support tickets
- **Branding** — per-tenant customisation (logos, colours, tagline, trust badges)

### Branding Engine

The Theme Editor provides 6-tab customisation:
1. **Colours** — primary, accent, and semantic colours with WCAG contrast checking
2. **Fonts** — 13 Google/system font options with dynamic loading
3. **Logos** — main and compact logo upload
4. **Login** — configurable tagline, trust badges, copyright
5. **Email** — header colour, body background
6. **Advanced** — custom CSS injection

**Preset Themes:** Financial Green, Corporate Blue, Modern Dark, Classic Gold, Minimal Gray, High Contrast

---

## 25. Data Protection & Privacy

RegOS includes comprehensive data protection features aligned with GDPR and NDPR requirements:

### Core Capabilities

- **Data Subject Requests (DSARs)** — intake, tracking, and fulfilment of data access/deletion requests
- **Consent Management** — consent records with version tracking and re-consent flows
- **Data Breach Incident Tracking** — incident recording, assessment, and notification management
- **Data Processing Activities** — ROPA (Record of Processing Activities) maintenance
- **PII Anonymisation** — data anonymisation for analytics and exports
- **Data Residency** — `DataResidencyRouter` enforces geographic data storage requirements

### Privacy Dashboard

Unified privacy compliance view with metrics across all data protection obligations.

### Historical Data Migration

For institutions transitioning to RegOS:
- **Migration Home** — readiness status and import job upload
- **Mapping Editor** — map legacy fields to RegOS schema
- **Migration Tracker** — progress monitoring with job history
- Supports Excel, CSV, and PDF source formats

---

## 26. Compliance-as-a-Service (CaaS) API

RegOS exposes its compliance capabilities as an API for programmatic integration:

### Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/caas/validate` | POST | Validate data against module rules |
| `/api/v1/caas/submit` | POST | API-based return submission |
| `/api/v1/caas/templates/{moduleCode}` | GET | Template structure retrieval |
| `/api/v1/caas/deadlines` | GET | Filing deadline information |
| `/api/v1/caas/score` | POST | Compliance health score computation |
| `/api/v1/caas/changes` | GET | Regulatory change notifications |
| `/api/v1/caas/simulate` | POST | Scenario simulation |

### Features

- API key authentication with per-key rate limiting
- Per-tenant sliding window rate limiter
- Webhook dispatch for event notifications
- Auto-filing service for scheduled submissions
- CaaS admin endpoints for API key management

### Additional API Groups

| Group | Base Path | Purpose |
|-------|-----------|---------|
| Authentication | `/api/v1/auth` | Login, token refresh, revocation |
| Submissions | `/api/v1/submissions` | Submission CRUD |
| Templates | `/api/v1/templates` | Template and field management |
| Filing Calendar | `/api/v1/filing-calendar` | RAG status, deadlines |
| Compliance | `/api/v1/compliance` | Health scoring, queries |
| Direct Submission | `/api/v1/direct-submission` | Regulator API submission |
| Webhooks | `/api/v1/webhooks` | Endpoint management |
| Stress Testing | `/api/v1/stress-testing` | Test execution and reports |
| Policy Simulation | `/api/v1/policy-simulation` | Scenario modelling |
| Cross-Border | `/api/v1/cross-border` | Consolidated reporting |
| Data Protection | `/api/v1/data-protection` | GDPR/DSAR endpoints |
| Data Feeds | `/api/v1/data-feeds` | External data integration |
| Historical Migration | `/api/v1/historical-migration` | Data migration |
| Schema | `/api/v1/schema` | XSD endpoints |

---

## 27. Platform Intelligence Workspace

The Admin Portal includes a centralised intelligence hub for platform operators:

### Intelligence Hub

- **Catalog management** — Capital, Operational Resilience, Model Risk workspaces
- **Marketplace rollout tracking** — module deployment across tenants
- **Platform operations oversight** — system health and performance
- **Institutional supervisory catalog** — per-institution supervisory profiles

### Refresh Management

- `PlatformIntelligenceRefreshService` — data freshness orchestration
- `PlatformIntelligenceRefreshJob` — scheduled background refresh
- Run history tracking for audit

### Executive Dashboards

Role-specific intelligence views:

| Dashboard | Audience |
|-----------|----------|
| **Governor Dashboard** | Central bank leadership — systemic view |
| **Deputy Governor Dashboard** | Deputy governor — oversight focus |
| **Executive Dashboard** | Institution executives — entity performance |
| **Examiner Dashboard** | Examination teams — operational workspace |

### Module Analytics

Per-module deep-dive analytics with:
- Submission volume and timeliness
- Validation pass rates
- Data quality trends
- Cross-institutional comparison

---

## 28. Design System & Accessibility

### Design Tokens

RegOS uses CSS custom properties with the `--cbn-*` prefix:
- **Primary:** `#006B3F` (green) — brand identity
- **Accent:** `#C8A415` (gold) — highlights and CTAs
- **Spacing:** 8px grid (`--space-0` through `--space-24`)
- **Motion:** `--motion-instant` (100ms) / `fast` (200ms) / `normal` (300ms) / `slow` (500ms)
- **Easing:** `--ease-out`, `--ease-spring`, `--ease-bounce`

### Shared Component Library

The platform includes 50+ shared components:

**Navigation & Layout:**
- `PageHeader` / `PortalPageHeader` — universal sticky header with breadcrumbs, tabs, compact scroll mode, and overflow actions
- `CommandPalette` — `Ctrl+K` command search with ARIA-live result count
- `ProgressStepper` — wizard step indicator with clickable back-navigation

**Data Display:**
- `DataTable` — full-featured table with sorting, filtering, column customisation, density modes, presets, resize, drag-to-reorder, and export
- `SkeletonLoader` — 14 shimmer variants (Text, Heading, Stat, Card, ChartBar, CalendarGrid, FormField, etc.)
- `SkeletonGuard` — loading guard with timeout and retry
- `Sparkline` — inline micro-charts
- `AnimatedCounter` — animated number transitions

**Forms & Input:**
- `FloatingInput` — floating label inputs
- `FormField` — accessible field wrapper with error/hint IDs and `aria-describedby`
- `AutoSaveManager` — debounced auto-save with navigation guard and dirty state tracking
- `AutoSaveIndicator` — reactive status chip (Dirty → Saving → Saved → Failed)
- `SlugField` — auto-generated slug from source value
- `PeriodPicker` — date range/period selector
- `CurrencyInput` — formatted currency input
- `RichTextEditor` — rich text editing
- `SpringToggle` — animated toggle switch

**Feedback:**
- `ToastContainer` — toast notifications with `role="alert"` and `aria-live="assertive"`
- `EmptyState` — context-specific SVG illustrations (12 variants)
- `ErrorState` — in-page failure with exponential backoff auto-retry
- `SectionErrorBoundary` — section-level error catch with auto-retry (1s/2s/4s backoff, max 3)
- `GlobalErrorBanner` — dismissible top banner for non-critical errors
- `ConfettiOverlay` — celebration animation

**Overlays:**
- `AppModal` — accessible modal with focus trap
- `ConfirmDialog` — confirmation dialog
- `ContextualHelpDrawer` — slide-out help panel
- `KeyboardShortcutsOverlay` — shortcut reference (triggered by `?`)
- `ShortcutsOverlay` — portal shortcut reference

**Drag & Drop:**
- `DragList` — generic sortable list
- `FileDropZone` — file upload drop zone
- `SubmissionKanban` — kanban board for submissions

### Accessibility

RegOS implements WCAG 2.1 AA compliance:

- **Skip links** — content, navigation, and search skip targets
- **Route announcer** — `aria-live` region announces page changes
- **Focus trap** — modals and dialogs trap focus (`FCAccessibility.trapFocus/releaseFocus`)
- **Keyboard shortcuts** — full keyboard navigation system:
  - `G` chord: `G+D` (dashboard), `G+T` (templates), `G+S` (submissions), etc.
  - `?` opens shortcut overlay
  - `Ctrl+K` opens command palette
  - `Ctrl+S` saves forms
  - Arrow keys navigate tables (`data-keyboard-nav`)
- **High contrast** — `forced-colors` media query support
- **Colour-blind safe** — status shapes supplement colour coding
- **Touch targets** — 44px minimum
- **200% zoom** — layout tested at 200% browser zoom
- **Screen reader** — semantic HTML, ARIA attributes, live regions
- **Reduced motion** — `prefers-reduced-motion` disables animations

### Error Handling Architecture

```
Component Level:  SectionErrorBoundary → auto-retry with backoff
Page Level:       ErrorBoundary wrapping @Body → sidebar/topbar remain visible
Non-Critical:     GlobalErrorService → dismissible top banner
Action Errors:    Toast.Error() → transient notification
Network:          OfflineBanner → fixed top indicator
Session:          SessionExpiredModal → warning countdown → auth redirect
Circuit:          Disconnect overlay → auto-reconnect
```

---

## 29. Security & Infrastructure

### Authentication & Session Security

- HTTP-only, SameSite=Strict, Secure cookies
- JWT with RSA signing (access + refresh tokens)
- MFA enforcement (TOTP + SMS + backup codes)
- Account lockout (5 failed attempts, 15-minute window)
- Session lifecycle management with cross-tab synchronisation
- Open redirect prevention on login/reset flows

### API Security

- API key authentication with per-key permissions and rate limiting
- Per-tenant sliding window rate limiter (tiered by subscription)
- Security headers middleware (X-Content-Type-Options, X-Frame-Options, CSP, HSTS, Referrer-Policy)
- HTTPS enforcement with HSTS in production
- Antiforgery token protection

### Data Security

- Multi-tenant row-level security via SQL Server SESSION_CONTEXT
- Field-level encryption for sensitive data
- Digital signatures (RSA-SHA512, ECDSA-SHA384)
- SHA-256 hash verification for exports and evidence packages
- Webhook HMAC signature verification

### Audit Trail

- Comprehensive audit logging via `IAuditLogger`
- Field-level change history (before/after with user attribution)
- Submission timeline with every status change
- Impersonation audit logging
- API key usage tracking

### Operational

- Structured logging via Serilog with log context enrichment
- Health check endpoints on API
- Background job scheduling (ForeSight, intelligence refresh, auto-filing)

---

## 30. Technology Stack

| Layer | Technology |
|-------|-----------|
| **Frontend** | Blazor Server (SSR), CSS custom properties, vanilla JS interop |
| **Backend** | ASP.NET Core 8, C# 12, Minimal APIs |
| **ORM** | Entity Framework Core (migrations) + Dapper (queries) |
| **Database** | SQL Server with row-level security |
| **Authentication** | Cookie-based (portals), JWT (API), SAML 2.0 (SSO) |
| **AI/ML** | Anthropic Claude (LLM), custom anomaly detection, statistical models |
| **Logging** | Serilog (structured, console + configurable sinks) |
| **Email** | SMTP |
| **SMS** | Twilio / configurable provider |
| **Architecture** | Clean Architecture (Domain → Application → Infrastructure → Presentation) |
| **Multi-Tenancy** | SQL Server SESSION_CONTEXT RLS, tenant middleware |
| **Containerisation** | Docker with docker-compose |

### Project Structure

```
FC Engine/
├── src/
│   ├── FC.Engine.Domain/          # 108 entities, enums, value objects, abstractions
│   ├── FC.Engine.Application/     # Orchestrators, DTOs, service interfaces
│   ├── FC.Engine.Infrastructure/  # 130+ service implementations, persistence, integrations
│   ├── FC.Engine.Admin/           # Blazor Server — regulator/admin portal (37+ pages)
│   ├── FC.Engine.Portal/          # Blazor Server — institution portal (82+ pages)
│   └── FC.Engine.Api/             # REST API — 20+ endpoint groups
├── tests/
│   ├── FC.Engine.Application.Tests/
│   ├── FC.Engine.Infrastructure.Tests/
│   └── FC.Engine.Domain.Tests/
└── docker/
    └── docker-compose.yml
```

### Scale

| Metric | Count |
|--------|-------|
| Domain Entities | 108 |
| Infrastructure Services | 130+ |
| Admin Portal Pages | 37+ |
| Institution Portal Pages | 82+ |
| API Endpoint Groups | 20+ |
| Shared UI Components | 50+ |
| Database Migrations | 40+ |

---

## Summary

RegOS is a comprehensive regulatory operating system that digitises the full supervisory lifecycle:

**For Regulators:**
- Design and publish return templates with complex validation rules
- Monitor institutional compliance in real-time across dashboards
- Leverage AI-powered intelligence (ForeSight predictions, anomaly detection, RegulatorIQ chat)
- Conduct stress tests and policy simulations before implementation
- Manage examinations with auto-generated briefing packs
- Track systemic risk, contagion, and early warning signals
- Supervise cross-border and consolidated institutions

**For Financial Institutions:**
- Submit returns via XML upload, online form, bulk upload, or API
- Validate data in real-time with five-phase validation
- Track filing deadlines with calendar and escalation alerts
- Manage maker-checker approval workflows
- Access compliance analytics, ForeSight predictions, and anomaly alerts
- Generate compliance certificates and evidence packages
- Customise branding and manage team access

**For Partners:**
- White-label the platform for client institutions
- Monitor portfolio health across sub-tenants
- Manage support escalation
- Access CaaS APIs for programmatic integration

The platform combines traditional regulatory technology with modern AI capabilities, providing a single system of record for financial supervision across jurisdictions.
