# RegOS Platform - Personas, Interaction Points and User Stories

> RegOS is a multi-tenant regulatory operations platform spanning platform control, filing operations, supervisory review, partner enablement, and public trust workflows.
> This document maps the personas implemented across the current RegOS codebase to their primary workspaces, interaction points, and user stories.

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Implementation Notes](#implementation-notes)
3. [Persona Summary Matrix](#persona-summary-matrix)
4. [Persona 1 - Platform Administrator](#persona-1---platform-administrator)
5. [Persona 2 - Template and Rule Manager](#persona-2---template-and-rule-manager)
6. [Persona 3 - Audit and Read-Only Reviewer](#persona-3---audit-and-read-only-reviewer)
7. [Persona 4 - Governor](#persona-4---governor)
8. [Persona 5 - Deputy Governor or Supervisory Head](#persona-5---deputy-governor-or-supervisory-head)
9. [Persona 6 - Executive Oversight User](#persona-6---executive-oversight-user)
10. [Persona 7 - Examiner or Supervisory Analyst](#persona-7---examiner-or-supervisory-analyst)
11. [Persona 8 - Institution Administrator](#persona-8---institution-administrator)
12. [Persona 9 - Institution Maker](#persona-9---institution-maker)
13. [Persona 10 - Institution Checker](#persona-10---institution-checker)
14. [Persona 11 - Institution Approver](#persona-11---institution-approver)
15. [Persona 12 - Institution Viewer](#persona-12---institution-viewer)
16. [Persona 13 - White-Label Partner Administrator](#persona-13---white-label-partner-administrator)
17. [Persona 14 - Anonymous Whistleblower](#persona-14---anonymous-whistleblower)
18. [Persona 15 - External Certificate Verifier](#persona-15---external-certificate-verifier)
19. [Cross-Cutting Interaction Points](#cross-cutting-interaction-points)
20. [Workflow State Machines](#workflow-state-machines)
21. [Capability Matrix](#capability-matrix)

---

## System Overview

RegOS supports the full operating model around regulatory data collection and supervision:

```text
Platform Setup -> Tenant Onboarding -> Licence and Module Activation ->
Template and Rule Governance -> Institution Onboarding ->
Return Preparation -> Validation -> Maker-Checker Review ->
Direct or Batch Submission -> Regulator Receipt and Query Handling ->
Supervisory Intelligence -> Partner Oversight -> Public Verification and Reporting
```

**Primary User Types**

| User Type | Description | Typical Access Surface |
|-----------|-------------|------------------------|
| `PLATFORM_USER` | Tenantless control-plane operator | Admin portal platform workspace |
| `PORTAL_GOVERNANCE_USER` | Internal content, rule, and review user | Admin portal data management workspace |
| `REGULATOR_USER` | Tenant-scoped supervisory user | Admin portal regulator workspace |
| `INSTITUTION_USER` | Reporting institution user | Institution portal |
| `PARTNER_USER` | White-label partner user | Institution portal partner workspace |
| `PUBLIC_USER` | Anonymous external party | Public verification and whistleblower pages |

**Implemented Technical Boundaries**

| Boundary | Current Mechanism | Notes |
|----------|-------------------|-------|
| `PlatformAdmin` | Tenantless admin portal user elevated to `PlatformAdmin` | Full cross-tenant access including impersonation |
| `Admin`, `Approver`, `Viewer` | Admin portal role catalog | Used for content and governance workflows; tenantless users are still elevated to `PlatformAdmin` claims |
| `RegulatorOnly` | Authenticated user on a regulator tenant | Governor, deputy, executive, and examiner personas currently share this boundary |
| `InstitutionAdmin`, `InstitutionMaker`, `InstitutionChecker` | Institution portal policies | Route-level segregation is strongest for admin, maker, and checker |
| `InstitutionRole.Approver` | Role catalog plus permission seeds | Persona exists; routed UI separation is still partial |
| `AllowAnonymous` | Public pages only | Used for whistleblower intake/status and certificate verification |

---

## Implementation Notes

1. Regulator dashboards such as Governor, Deputy Governor, Executive, and Examiner are distinct stakeholder views, but they currently sit behind the same `RegulatorOnly` authorization boundary.
2. White-label partner functionality reuses the institution portal and is currently exposed to partner tenants through the `Admin` role plus partner-tenant checks.
3. The institution `Approver` role is seeded in the role catalog, surfaced in team management and notifications, and has approval permissions, but some routed approval screens are still checker-led in the current UI.
4. This document uses page routes as interaction points because that is the clearest implemented surface across the current codebase.

---

## Persona Summary Matrix

| Persona | User Type | Auth Boundary | Primary Workspace | Primary Responsibility |
|---------|-----------|---------------|-------------------|------------------------|
| Platform Administrator | `PLATFORM_USER` | `PlatformAdmin` | Platform control plane | Tenant, module, billing, feature, and health operations |
| Template and Rule Manager | `PORTAL_GOVERNANCE_USER` | `Admin` or `Approver` | Admin portal governance pages | Template, formula, rule, and submission governance |
| Audit and Read-Only Reviewer | `PORTAL_GOVERNANCE_USER` | `Viewer` | Admin portal read surfaces | Read-only oversight and evidence review |
| Governor | `REGULATOR_USER` | `RegulatorOnly` | Governor dashboard | Strategic sector-wide oversight |
| Deputy Governor or Supervisory Head | `REGULATOR_USER` | `RegulatorOnly` | Deputy dashboard | Escalation, intervention, and portfolio supervision |
| Executive Oversight User | `REGULATOR_USER` | `RegulatorOnly` | Executive dashboard | Management reporting across compliance domains |
| Examiner or Supervisory Analyst | `REGULATOR_USER` | `RegulatorOnly` | Regulator workspace and inbox | Filing review, examination work, and supervisory analytics |
| Institution Administrator | `INSTITUTION_USER` | `InstitutionAdmin` | Admin dashboard and institution settings | Team management, settings, branding, and reporting control |
| Institution Maker | `INSTITUTION_USER` | `InstitutionMaker` | Submission and validation workspace | Prepare, validate, and submit returns |
| Institution Checker | `INSTITUTION_USER` | `InstitutionChecker` | Approval queue | Review, approve, or reject maker submissions |
| Institution Approver | `INSTITUTION_USER` | Role catalog plus permission seed | Shared institution surfaces | Final sign-off where four-eyes or delegated approval is required |
| Institution Viewer | `INSTITUTION_USER` | Authenticated institution access | Read-only dashboard and reports | Monitor deadlines, reports, and filing posture |
| White-Label Partner Administrator | `PARTNER_USER` | Partner tenant `Admin` | Partner dashboard | Manage sub-tenants, branding, and support escalations |
| Anonymous Whistleblower | `PUBLIC_USER` | `AllowAnonymous` | Public whistleblower pages | Submit confidential conduct reports and check status |
| External Certificate Verifier | `PUBLIC_USER` | Anonymous public verification | Public verification page | Validate compliance certificate authenticity |

---

## Persona 1 - Platform Administrator

**Role Code:** `PLATFORM_ADMIN`  
**User Type:** `PLATFORM_USER`  
**Authorization Boundary:** `PlatformAdmin`

### Profile

The Platform Administrator runs the RegOS control plane. This persona provisions tenants, manages platform-level modules and billing, operates rollout controls, monitors platform health, and uses cross-tenant intelligence workspaces.

### Primary Dashboards and Workspaces

- Platform dashboard: `/dashboard/platform`
- Tenant control: `/platform/tenants`, `/platform/tenant-setup`, `/platform/tenants/{TenantId}`
- Partner onboarding: `/platform/partners/onboard`
- Registry and rollout: `/modules`, `/modules/import`, `/licence-types`, `/jurisdictions`, `/platform/templates`
- Commercial ops: `/platform/billing-ops`, `/billing/plans`, `/billing/subscriptions`, `/billing/invoices`, `/billing/revenue`
- Platform intelligence: `/platform/intelligence`, `/platform/foresight`, `/platform/module-analytics`, `/platform/systemic-risk`, `/platform/compliance-health`
- Capital workspaces: `/platform/capital`, `/platform/capital/planning`, `/platform/capital/rwa`, `/platform/capital/buffers`, `/platform/capital/stack`, `/platform/capital/actions`
- Operational controls: `/platform/health`, `/platform/feature-flags`, `/platform/regulatory-calendar`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review control-plane posture | `/dashboard/platform` | Cross-tenant KPIs, growth, health, and risk posture |
| Provision or suspend tenants | `/platform/tenants`, `/platform/tenant-setup`, `/platform/tenants/{TenantId}` | Create tenants, assign status, and manage detail |
| Onboard partners | `/platform/partners/onboard` | Create white-label partner organizations and agreements |
| Manage rollout catalog | `/modules`, `/modules/import`, `/platform/templates`, `/licence-types`, `/jurisdictions` | Control module availability and jurisdiction coverage |
| Run billing operations | `/platform/billing-ops`, `/billing/*` | Plans, subscriptions, invoicing, and revenue workflows |
| Operate platform controls | `/platform/health`, `/platform/feature-flags`, `/platform/regulatory-calendar` | Health, feature rollout, and calendar imports |
| Use cross-tenant intelligence | `/platform/intelligence`, `/platform/foresight`, `/platform/module-analytics` | Platform-wide insight and prioritization |
| Run capital planning | `/platform/capital/*` | Supervisory capital simulation and what-if planning |

### User Stories

```text
US-PLA-001: As a Platform Administrator, I want to onboard a new tenant with the right licence mix
            so that the correct modules and filing obligations become active immediately.

US-PLA-002: As a Platform Administrator, I want to suspend or reactivate a tenant
            so that access can be aligned to commercial, legal, or operational decisions.

US-PLA-003: As a Platform Administrator, I want to import modules and manage licence mappings
            so that the platform can support additional regulators and reporting packs.

US-PLA-004: As a Platform Administrator, I want to control plans, subscriptions, invoices, and revenue
            so that RegOS commercial operations stay synchronized with tenant entitlements.

US-PLA-005: As a Platform Administrator, I want to monitor platform health and feature flags
            so that I can roll out changes safely and respond before outages spread.

US-PLA-006: As a Platform Administrator, I want to impersonate or inspect tenant contexts when required
            so that support and rollout issues can be diagnosed without direct tenant intervention.
```

---

## Persona 2 - Template and Rule Manager

**Role Code:** `ADMIN` or `APPROVER`  
**User Type:** `PORTAL_GOVERNANCE_USER`  
**Authorization Boundary:** Authenticated admin portal governance access

### Profile

The Template and Rule Manager owns regulatory content governance inside RegOS. They create and review templates, formulas, cross-sheet rules, business rules, and submission evidence required for controlled publishing.

### Primary Dashboards and Workspaces

- Template management: `/templates`, `/templates/{ReturnCode}`
- Formula catalog: `/formulas`
- Cross-sheet rules: `/cross-sheet-rules`, `/cross-sheet-rules/{Id}`
- Business rules: `/business-rules`
- Submission review: `/submissions`, `/submissions/{Id}`, `/submissions/kanban`, `/submissions/drafts`
- Change impact and audit: `/impact-analysis`, `/audit`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Manage templates | `/templates`, `/templates/{ReturnCode}` | Create drafts, edit fields, submit for review, publish |
| Maintain formulas | `/formulas` | Define intra-sheet validation logic |
| Maintain cross-sheet logic | `/cross-sheet-rules`, `/cross-sheet-rules/{Id}` | Manage inter-template reconciliation rules |
| Maintain business rules | `/business-rules` | Configure non-structural validation logic |
| Review operational effects | `/impact-analysis` | Estimate downstream impact of template changes |
| Review submissions | `/submissions`, `/submissions/{Id}`, `/submissions/kanban` | Inspect validation outcomes and pipeline posture |
| Inspect evidence trail | `/audit` | Confirm change history and publishing evidence |

### User Stories

```text
US-TRM-001: As a Template and Rule Manager, I want to create a draft version of a return template
            so that regulatory changes can be prepared without disrupting live submissions.

US-TRM-002: As a Template and Rule Manager, I want to define formulas and business rules
            so that incorrect or incomplete returns are stopped before acceptance.

US-TRM-003: As a Template and Rule Manager, I want to review cross-sheet dependencies
            so that related templates remain internally consistent across a filing pack.

US-TRM-004: As a Template and Rule Manager, I want to run impact analysis before publishing
            so that downstream data tables, institutions, and reports are not broken unexpectedly.

US-TRM-005: As a Template and Rule Manager, I want to inspect real submission outcomes
            so that I can refine templates and rules based on recurring validation failures.
```

---

## Persona 3 - Audit and Read-Only Reviewer

**Role Code:** `VIEWER`  
**User Type:** `PORTAL_GOVERNANCE_USER`  
**Authorization Boundary:** Read-only admin portal access

### Profile

The Audit and Read-Only Reviewer consumes administrative data without making structural changes. This persona is typically used for assurance, oversight, support observation, and controlled evidence review.

### Primary Dashboards and Workspaces

- Dashboard: `/`
- Template and rule browsing: `/templates`, `/formulas`, `/cross-sheet-rules`, `/business-rules`
- Submission browsing: `/submissions`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review system posture | `/` | High-level statistics and current state |
| Browse template catalog | `/templates` | Read-only view of active and draft content |
| Review validation logic | `/formulas`, `/cross-sheet-rules`, `/business-rules` | Inspect current logic without editing |
| Review submission outcomes | `/submissions` | Read-only inspection of filing patterns and validation results |

### User Stories

```text
US-ARV-001: As a Read-Only Reviewer, I want to inspect template and rule definitions
            so that I can understand how the current control framework is configured.

US-ARV-002: As a Read-Only Reviewer, I want to review submission histories
            so that I can observe filing quality without changing operational data.

US-ARV-003: As a Read-Only Reviewer, I want to access dashboards and catalogs without edit rights
            so that assurance and support review can happen safely.
```

---

## Persona 4 - Governor

**Persona Code:** `REGULATOR_GOVERNOR`  
**User Type:** `REGULATOR_USER`  
**Authorization Boundary:** `RegulatorOnly`

### Profile

The Governor uses RegOS as a strategic command view across the supervised sector. This persona consumes sector health, capital pressure, intervention burden, and top-risk institution summaries rather than case-level workflow details.

### Primary Dashboards and Workspaces

- Governor dashboard: `/regulator/dashboards/governor`
- Strategic risk views: `/regulator/sector-health`, `/regulator/systemic-risk`, `/regulator/contagion`, `/regulator/stress-test`
- Intelligence assistance: `/regulator/complianceiq`, `/regulator/regulatoriq`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| View strategic dashboard | `/regulator/dashboards/governor` | Sector-wide capital, compliance, resilience, and risk posture |
| Review systemic concentration | `/regulator/systemic-risk`, `/regulator/contagion` | Cross-institution systemic exposure indicators |
| Review sector heat and stability | `/regulator/sector-health`, `/regulator/stress-test` | Stress and resilience posture across supervised entities |
| Query intelligence workspace | `/regulator/complianceiq`, `/regulator/regulatoriq` | Natural-language access to supervisory context |

### User Stories

```text
US-GOV-001: As the Governor, I want to see top-risk institutions and sector concentration signals
            so that I can understand systemic exposure at a glance.

US-GOV-002: As the Governor, I want to see capital, resilience, and compliance trends together
            so that strategic interventions reflect the full supervisory picture.

US-GOV-003: As the Governor, I want to use intelligence workspaces to ask high-level questions
            so that I can shorten briefing preparation time.
```

---

## Persona 5 - Deputy Governor or Supervisory Head

**Persona Code:** `REGULATOR_DEPUTY`  
**User Type:** `REGULATOR_USER`  
**Authorization Boundary:** `RegulatorOnly`

### Profile

The Deputy Governor or Supervisory Head focuses on active interventions, escalations, and remediation velocity. This persona is closer to execution than the Governor and is expected to track overdue actions across the supervised portfolio.

### Primary Dashboards and Workspaces

- Deputy dashboard: `/regulator/dashboards/deputy`
- Risk workspaces: `/regulator/warnings`, `/regulator/anomalies`, `/regulator/analytics`
- Escalation views: `/regulator/inbox`, `/regulator/institution/{InstitutionId}`
- Policy and obligations: `/regulator/policies`, `/regulator/compliance/navigator`, `/regulator/compliance/obligations`, `/regulator/compliance/impact`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review intervention backlog | `/regulator/dashboards/deputy` | Escalations, remediation rate, overdue queue |
| Work warning and anomaly signals | `/regulator/warnings`, `/regulator/anomalies`, `/regulator/analytics` | Prioritize supervisory response |
| Drill into institutions | `/regulator/institution/{InstitutionId}` | See institution-level posture and activity |
| Track incoming filings | `/regulator/inbox` | Review filings needing attention |
| Review policy and obligation posture | `/regulator/policies`, `/regulator/compliance/*` | Align interventions to policy intent |

### User Stories

```text
US-DGH-001: As a Deputy Governor, I want to see which interventions are overdue or due soon
            so that management action can be focused where slippage is highest.

US-DGH-002: As a Deputy Governor, I want to drill into flagged institutions quickly
            so that supervisory conversations are evidence-led rather than anecdotal.

US-DGH-003: As a Deputy Governor, I want to compare domain pressure across compliance, resilience, and capital
            so that remediation efforts are prioritized coherently.

US-DGH-004: As a Deputy Governor, I want policy obligations and live institution posture in one workflow
            so that supervisory enforcement is tied to explicit rules and standards.
```

---

## Persona 6 - Executive Oversight User

**Persona Code:** `REGULATOR_EXECUTIVE`  
**User Type:** `REGULATOR_USER`  
**Authorization Boundary:** `RegulatorOnly`

### Profile

The Executive Oversight User is a senior management consumer of regulatory performance information. This persona uses executive rollups for briefings, management committees, and operating reviews rather than deep case handling.

### Primary Dashboards and Workspaces

- Executive dashboard: `/regulator/dashboards/executive`
- Sector summaries: `/regulator/sector-health`, `/regulator/heatmap`
- Intelligence summaries: `/regulator/complianceiq`, `/regulator/regulatoriq`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review executive scorecards | `/regulator/dashboards/executive` | Filing compliance, open findings, next deadlines |
| Review visual summaries | `/regulator/sector-health`, `/regulator/heatmap` | Sector conditions and hotspot summaries |
| Request narrative context | `/regulator/complianceiq`, `/regulator/regulatoriq` | Ask for concise decision support |

### User Stories

```text
US-EXE-001: As an Executive Oversight user, I want a concise briefing view of filing compliance and open findings
            so that I can report status without navigating operational detail.

US-EXE-002: As an Executive Oversight user, I want visual heatmaps and institution rankings
            so that management attention can be directed to the right entities quickly.

US-EXE-003: As an Executive Oversight user, I want narrative intelligence support
            so that board and committee packs can be assembled faster.
```

---

## Persona 7 - Examiner or Supervisory Analyst

**Persona Code:** `REGULATOR_EXAMINER`  
**User Type:** `REGULATOR_USER`  
**Authorization Boundary:** `RegulatorOnly`

### Profile

The Examiner or Supervisory Analyst is the main operational regulator persona in RegOS. This persona handles incoming submissions, institution drill-downs, examination work, policy analysis, sanctions and conduct workflows, resilience cases, and cross-border supervision.

### Primary Dashboards and Workspaces

- Examiner dashboard: `/regulator/dashboards/examiner`
- Filing operations: `/regulator/inbox`, `/regulator/inbox/{SubmissionId}`, `/regulator/workspace`, `/regulator/institution/{InstitutionId}`
- Risk intelligence: `/regulator/warnings`, `/regulator/analytics`, `/regulator/anomalies`, `/regulator/heatmap`, `/regulator/contagion`, `/regulator/stress-test`
- AI assistance: `/regulator/complianceiq`, `/regulator/regulatoriq`
- Policy and standards: `/regulator/policies*`, `/regulator/compliance/*`, `/regulator/models/*`
- Integrity and resilience: `/regulator/surveillance`, `/regulator/sanctions*`, `/regulator/whistleblower/cases`, `/regulator/resilience/*`
- Cross-border: `/regulator/cross-border/*`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Receive and review filings | `/regulator/inbox`, `/regulator/inbox/{SubmissionId}` | Triage regulator receipts, review details, raise queries |
| Investigate supervised entities | `/regulator/institution/{InstitutionId}`, `/regulator/workspace` | Institution drill-down and examination work |
| Work anomaly and warning queues | `/regulator/warnings`, `/regulator/anomalies`, `/regulator/heatmap`, `/regulator/contagion`, `/regulator/stress-test` | Signal-led supervision |
| Run policy analysis | `/regulator/policies*`, `/regulator/compliance/*`, `/regulator/models/*` | Simulate policy changes and govern standards |
| Run integrity workflows | `/regulator/surveillance`, `/regulator/sanctions*`, `/regulator/whistleblower/cases`, `/regulator/resilience/*` | Conduct, AML, sanctions, whistleblower, and resilience work |
| Run cross-border supervision | `/regulator/cross-border/*` | Mappings, data flows, consolidation, divergences, and deadlines |
| Use intelligence copilot | `/regulator/complianceiq`, `/regulator/regulatoriq` | Ask regulator-context questions and retrieve evidence |

### User Stories

```text
US-EXM-001: As an Examiner, I want to open a submission from the regulator inbox and inspect its supervisory history
            so that I can decide whether to accept it, query it, or escalate it.

US-EXM-002: As an Examiner, I want to open an institution drill-down with live risk context
            so that case assessment is grounded in current filings and intervention history.

US-EXM-003: As an Examiner, I want anomaly, stress, and contagion views in the same workspace
            so that I can move from signal detection to supervisory action without switching tools.

US-EXM-004: As an Examiner, I want to manage policy simulations, consultation responses, and obligation navigation
            so that standards work and live supervision can reinforce each other.

US-EXM-005: As an Examiner, I want to work sanctions, conduct, whistleblower, resilience, and cross-border cases
            so that non-filing supervisory risks are handled in the same platform.

US-EXM-006: As an Examiner, I want AI-assisted regulator queries and summaries
            so that evidence gathering and briefing preparation are faster.
```

---

## Persona 8 - Institution Administrator

**Role Code:** `ADMIN`  
**User Type:** `INSTITUTION_USER`  
**Authorization Boundary:** `InstitutionAdmin`

### Profile

The Institution Administrator owns the reporting operating model inside a supervised entity. This persona manages the institution team, settings, branding, subscription posture, onboarding assets, and executive reporting.

### Primary Dashboards and Workspaces

- Home and admin views: `/`, `/dashboard/admin`, `/dashboard/compliance`, `/dashboard/health`
- Institution control: `/institution`, `/institution/team`, `/institution/settings`
- Account settings: `/settings/branding`, `/settings/notifications`, `/settings/webhooks`
- Reporting and governance: `/reports/builder`, `/reports/board-pack`, `/reports/saved`, `/reports/audit`
- Subscription and modules: `/subscription/my-plan`, `/subscription/modules`, `/subscription/invoices`, `/subscription/payments`, `/modules`
- Enablement: `/onboarding/checklist`, `/onboarding/wizard`, `/onboarding/sandbox`, `/onboarding/tour/{Role}`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Manage institution profile | `/institution`, `/institution/settings` | Core organization and workflow configuration |
| Manage team and roles | `/institution/team` | Add Makers, Checkers, Approvers, Viewers, and Admins |
| Manage branding and integrations | `/settings/branding`, `/settings/webhooks`, `/settings/notifications` | Branding, notifications, and webhook setup |
| Review executive reports | `/reports/builder`, `/reports/board-pack`, `/reports/saved`, `/reports/audit` | Internal governance and board reporting |
| Monitor subscription posture | `/subscription/*`, `/modules` | Plan, invoices, payments, and module entitlement |
| Run enablement | `/onboarding/*` | Checklist, sandbox, tours, and onboarding wizard |

### User Stories

```text
US-IAD-001: As an Institution Administrator, I want to create and manage reporting users by role
            so that maker-checker separation and least privilege can be enforced.

US-IAD-002: As an Institution Administrator, I want to configure institution settings, branding, and webhooks
            so that RegOS fits our operating model and outward communications.

US-IAD-003: As an Institution Administrator, I want to monitor subscription and module posture
            so that the institution only relies on capabilities that are actually entitled.

US-IAD-004: As an Institution Administrator, I want to produce board and compliance reports
            so that executives can see filing posture without entering workflow detail.

US-IAD-005: As an Institution Administrator, I want a training sandbox and guided tours
            so that new staff can become productive without risking live submissions.
```

---

## Persona 9 - Institution Maker

**Role Code:** `MAKER`  
**User Type:** `INSTITUTION_USER`  
**Authorization Boundary:** `InstitutionMaker`

### Profile

The Institution Maker is the primary filing operator. This persona prepares returns, uploads XML, uses dynamic forms, runs validations, reviews anomalies, asks ComplianceIQ questions, and submits filings into the approval pipeline.

### Primary Dashboards and Workspaces

- Submission launch: `/submit`, `/submit/bulk`, `/submit/form/{ReturnCode}`
- Validation: `/validation/hub`, `/validation/cross-sheet`, `/validate`
- Filing history: `/submissions`, `/submissions/{SubmissionId}`, `/submissions/batches*`
- Calendars and references: `/calendar`, `/templates`, `/schemas`, `/help`
- Analytics: `/analytics/foresight`, `/analytics/anomalies`, `/analytics/chat`
- Consultation response: `/consultations`, `/consultations/{ConsultationId}/feedback`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Start filing | `/submit`, `/submit/bulk`, `/submit/form/{ReturnCode}` | XML upload, bulk upload, or form-based entry |
| Run validation | `/validation/hub`, `/validation/cross-sheet`, `/validate` | Pre-submit validation and remediation |
| Track submissions | `/submissions`, `/submissions/{SubmissionId}`, `/submissions/batches*` | Review status, reports, and receipts |
| Track obligations | `/calendar`, `/templates`, `/schemas` | Deadlines and reference material |
| Use analytics assistance | `/analytics/foresight`, `/analytics/anomalies`, `/analytics/chat` | Filing intelligence and question answering |
| Respond to consultations | `/consultations`, `/consultations/{ConsultationId}/feedback` | Provide institution feedback on proposed policy changes |

### User Stories

```text
US-MKR-001: As a Maker, I want to upload or key in a return and validate it before submission
            so that avoidable regulator rejections are reduced.

US-MKR-002: As a Maker, I want to see cross-sheet and anomaly issues before I hand off a filing
            so that downstream checker effort is spent on real judgement calls instead of preventable errors.

US-MKR-003: As a Maker, I want recent submissions, deadlines, and reference schemas in one portal
            so that I can manage filing work without switching systems.

US-MKR-004: As a Maker, I want ComplianceIQ and ForeSight guidance
            so that I can interpret validation patterns and prioritize remediation quickly.

US-MKR-005: As a Maker, I want to submit the institution's consultation feedback
            so that policy response becomes part of our normal regulatory workflow.
```

---

## Persona 10 - Institution Checker

**Role Code:** `CHECKER`  
**User Type:** `INSTITUTION_USER`  
**Authorization Boundary:** `InstitutionChecker`

### Profile

The Institution Checker provides the control function in the maker-checker workflow. This persona reviews pending submissions, consults audit evidence, approves or rejects filings, and communicates back to preparers.

### Primary Dashboards and Workspaces

- Approval queue: `/approvals`
- Submission detail: `/submissions`, `/submissions/{SubmissionId}`, `/submissions/{SubmissionId}/report`
- Audit and reports: `/reports/audit`, `/reports/compliance`, `/reports/builder`
- Notifications: `/notifications`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review pending queue | `/approvals` | Review submissions awaiting decision |
| Inspect evidence and validation | `/submissions/{SubmissionId}`, `/submissions/{SubmissionId}/report` | Understand issues before approval |
| Confirm audit trail | `/reports/audit` | See who changed what before sign-off |
| Communicate decisions | `/notifications` | Track review outcomes and follow-ups |
| Review broader posture | `/reports/compliance`, `/reports/builder` | Understand context before approval decisions |

### User Stories

```text
US-CHK-001: As a Checker, I want a queue of submissions awaiting review
            so that approval work is visible and orderly.

US-CHK-002: As a Checker, I want the full validation report and audit trail beside a submission
            so that I can decide confidently whether to approve or reject it.

US-CHK-003: As a Checker, I want to reject a submission with clear reviewer comments
            so that the Maker can correct the filing without ambiguity.

US-CHK-004: As a Checker, I want notifications when new filings enter my queue
            so that control deadlines are not missed.
```

---

## Persona 11 - Institution Approver

**Role Code:** `APPROVER`  
**User Type:** `INSTITUTION_USER`  
**Authorization Boundary:** Role exists in catalog; routed UI separation is partial

### Profile

The Institution Approver is intended for four-eyes or delegated final sign-off scenarios. In the current implementation this persona exists in team management, permission seeds, MFA rules, and notification targeting, but still shares much of the institution surface with other authenticated users.

### Primary Dashboards and Workspaces

- Shared institution workspace: `/`, `/submissions`, `/reports/compliance`, `/reports/builder`, `/notifications`
- Team assignment surface: `/institution/team`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review submission portfolio | `/submissions` | Monitor filing set requiring executive sign-off |
| Review reporting output | `/reports/compliance`, `/reports/builder` | Consume reporting evidence before sign-off |
| Receive escalations | `/notifications` | Follow approval-related or control-related alerts |
| Participate in role assignment | `/institution/team` | Approver role can be configured and assigned here |

### User Stories

```text
US-APR-001: As an Institution Approver, I want to be assigned as a final sign-off role
            so that higher-assurance filings can be approved under a separate control identity.

US-APR-002: As an Institution Approver, I want approval-related notifications routed to me
            so that escalated filings are not trapped in informal channels.

US-APR-003: As an Institution Approver, I want reporting evidence before I endorse a filing
            so that executive approval is based on the same facts seen by operations teams.
```

---

## Persona 12 - Institution Viewer

**Role Code:** `VIEWER`  
**User Type:** `INSTITUTION_USER`  
**Authorization Boundary:** Authenticated institution access

### Profile

The Institution Viewer is a read-only consumer of filing status, reporting posture, and regulatory guidance. This persona is typically used by internal oversight, finance leadership, or support stakeholders who need visibility but should not alter submissions.

### Primary Dashboards and Workspaces

- Dashboard and posture views: `/`, `/dashboard/compliance`, `/dashboard/health`
- Filing history: `/submissions`, `/submissions/{SubmissionId}`
- Reporting: `/reports/compliance`, `/reports/saved`
- Calendar and references: `/calendar`, `/templates`, `/schemas`, `/help`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review dashboard and health | `/`, `/dashboard/compliance`, `/dashboard/health` | View filing posture and trends |
| Review submissions | `/submissions`, `/submissions/{SubmissionId}` | Read-only filing status and history |
| Review reports | `/reports/compliance`, `/reports/saved` | Consume generated reports |
| Review reference material | `/calendar`, `/templates`, `/schemas`, `/help` | Deadlines, templates, and guidance |

### User Stories

```text
US-VIW-001: As an Institution Viewer, I want to monitor filing status and deadlines
            so that I can stay informed without changing operational data.

US-VIW-002: As an Institution Viewer, I want to open reports and saved analyses
            so that management review can happen inside the platform.

US-VIW-003: As an Institution Viewer, I want access to templates, schemas, and help material
            so that I can interpret filing requirements accurately.
```

---

## Persona 13 - White-Label Partner Administrator

**Role Code:** `ADMIN` on a `WhiteLabelPartner` tenant  
**User Type:** `PARTNER_USER`  
**Authorization Boundary:** Partner tenant plus admin role

### Profile

The White-Label Partner Administrator runs a partner portfolio of client tenants. This persona monitors client health, creates sub-tenants, manages partner-side client users, applies branding overrides, impersonates client contexts where allowed, and escalates support issues to platform operations.

### Primary Dashboards and Workspaces

- Partner dashboard: `/partner`, `/partner/dashboard`
- Sub-tenant management: `/partner/sub-tenants`, `/partner/sub-tenants/{SubTenantId}`
- Support escalation: `/partner/support`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Review partner portfolio | `/partner/dashboard` | Filing health, churn pressure, portfolio metrics |
| Create and manage sub-tenants | `/partner/sub-tenants` | Provision and monitor client institutions |
| Manage client detail | `/partner/sub-tenants/{SubTenantId}` | Users, submissions, and branding override |
| Escalate support issues | `/partner/support` | Raise and track partner support tickets |
| View as client tenant | Partner dashboard drill-down | Shift from partner view into client-operating context |

### User Stories

```text
US-WLP-001: As a White-Label Partner Admin, I want a portfolio dashboard across client tenants
            so that I can spot churn, inactivity, and filing pressure early.

US-WLP-002: As a White-Label Partner Admin, I want to create new sub-tenants with the right plan and licence profile
            so that client onboarding is standardized.

US-WLP-003: As a White-Label Partner Admin, I want to manage client users and branding from one place
            so that white-label delivery remains operationally efficient.

US-WLP-004: As a White-Label Partner Admin, I want to escalate support tickets into the platform
            so that incidents affecting client delivery are tracked formally.
```

---

## Persona 14 - Anonymous Whistleblower

**Persona Code:** `PUBLIC_WHISTLEBLOWER`  
**User Type:** `PUBLIC_USER`  
**Authorization Boundary:** `AllowAnonymous`

### Profile

The Anonymous Whistleblower is an external reporter using RegOS as a protected conduct-reporting channel. This persona can submit a confidential report without providing identifying information and can later check status using an anonymous token.

### Primary Dashboards and Workspaces

- Report intake: `/regulator/whistleblower/report`
- Status check: `/regulator/whistleblower/status/{AnonymousToken}`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Submit confidential report | `/regulator/whistleblower/report` | Choose regulator, category, institution, and narrative |
| Track case status | `/regulator/whistleblower/status/{AnonymousToken}` | One-way anonymous status tracking |

### User Stories

```text
US-WHB-001: As an Anonymous Whistleblower, I want to submit a conduct report without creating an account
            so that I can report concerns safely.

US-WHB-002: As an Anonymous Whistleblower, I want a one-way case token after submission
            so that I can track progress without revealing my identity.

US-WHB-003: As an Anonymous Whistleblower, I want to target the correct regulator during intake
            so that the report reaches the right supervisory desk immediately.
```

---

## Persona 15 - External Certificate Verifier

**Persona Code:** `PUBLIC_VERIFIER`  
**User Type:** `PUBLIC_USER`  
**Authorization Boundary:** Anonymous public verification

### Profile

The External Certificate Verifier is any outside party who needs to confirm whether a compliance certificate issued through RegOS is authentic and still valid.

### Primary Dashboards and Workspaces

- Public verification page: `/verify/{CertId}`

### Interaction Points

| Action | Route | Description |
|--------|-------|-------------|
| Verify certificate authenticity | `/verify/{CertId}` | Public read-only validation of certificate status and metadata |

### User Stories

```text
US-VFY-001: As an External Verifier, I want to check a certificate number without logging in
            so that I can validate authenticity quickly.

US-VFY-002: As an External Verifier, I want to see issuance and approval details for a valid certificate
            so that I can trust the compliance claim being presented to me.

US-VFY-003: As an External Verifier, I want a clear error state when a certificate is invalid or missing
            so that fraudulent or mistyped references can be identified immediately.
```

---

## Cross-Cutting Interaction Points

### Authentication, Security, and Consent

- Admin login and recovery: `/login`, `/forgot-password`, `/reset-password`, `/account/mfa-setup`
- Institution login and security: `/login`, `/change-password`, `/account/mfa-setup`, `/settings`
- Re-consent journeys: `/privacy/reconsent` in both portals
- Role-aware guided tours and onboarding: `/onboarding/checklist`, `/onboarding/tour/{Role}`, `/onboarding/sandbox`

### Collaboration and Notifications

- Institution notifications: `/notifications`
- Submission comments, reviewer decisions, and certificate/report pages under `/submissions/*` and `/reports/*`
- Regulator inbox, examiner queries, and supervisory review flows under `/regulator/inbox*`

### AI and Intelligence

- Institution intelligence: `/analytics/chat`, `/analytics/foresight`, `/analytics/anomalies`
- Regulator intelligence: `/regulator/complianceiq`, `/regulator/regulatoriq`
- Platform intelligence: `/platform/intelligence`, `/platform/foresight`, `/platform/module-analytics`

### Public Trust Surfaces

- Public certificate verification: `/verify/{CertId}`
- Anonymous whistleblower intake and status: `/regulator/whistleblower/report`, `/regulator/whistleblower/status/{AnonymousToken}`

---

## Workflow State Machines

### 1. Tenant Lifecycle

```text
PendingActivation -> Active -> Suspended -> Deactivated -> Archived
```

### 2. Template Governance Lifecycle

```text
Draft -> Review -> Published -> Deprecated -> Retired
```

### 3. Institution Filing Lifecycle

```text
Draft -> Parsing -> Validating -> Accepted / AcceptedWithWarnings / Rejected
      -> PendingApproval -> ApprovalRejected
      -> SubmittedToRegulator -> RegulatorAcknowledged -> RegulatorAccepted
      -> RegulatorQueriesRaised
```

### 4. Direct and Batch Regulatory Dispatch

```text
Direct submission:
Pending -> Packaging -> Signing -> Submitting -> Submitted -> Acknowledged
       -> Accepted / Rejected / QualityFeedback / Failed / RetryScheduled / Exhausted

Batch submission:
Pending -> Signing -> Dispatching -> Submitted -> Acknowledged
       -> Processing -> Accepted / QueriesRaised / FinalAccepted / Rejected / Failed
```

### 5. Supervisory Receipt and Query Lifecycle

```text
Regulator receipt:
Received -> UnderReview -> Accepted -> FinalAccepted
         -> QueriesRaised -> ResponseReceived

Examiner query:
Open -> Responded -> Resolved / Escalated
```

### 6. Consultation Lifecycle

```text
Draft -> Published -> Open -> Closed -> Aggregated
```

### 7. Partner Support Lifecycle

```text
Open -> InProgress -> Resolved -> Closed
```

---

## Capability Matrix

| Persona | Manage Tenants | Govern Templates and Rules | Submit Returns | Approve Returns | Run Regulator Workspaces | Manage Institution Team | Manage Partner Sub-Tenants | Public Anonymous Access |
|---------|----------------|----------------------------|----------------|-----------------|--------------------------|-------------------------|----------------------------|-------------------------|
| Platform Administrator | Yes | Yes | No | No | Via platform impersonation or regulator tenant context | No | Yes | No |
| Template and Rule Manager | No | Yes | No | No | No | No | No | No |
| Audit and Read-Only Reviewer | No | Read-only | No | No | No | No | No | No |
| Governor | No | No | No | No | Yes | No | No | No |
| Deputy Governor or Supervisory Head | No | No | No | No | Yes | No | No | No |
| Executive Oversight User | No | No | No | No | Yes | No | No | No |
| Examiner or Supervisory Analyst | No | No | No | No | Yes | No | No | No |
| Institution Administrator | No | No | Yes | Yes | No | Yes | No | No |
| Institution Maker | No | No | Yes | No | No | No | No | No |
| Institution Checker | No | No | No | Yes | No | No | No | No |
| Institution Approver | No | No | No | Partial in current routed UI | No | No | No | No |
| Institution Viewer | No | No | No | No | No | No | No | No |
| White-Label Partner Administrator | No | No | Indirect through sub-tenant support | Indirect through partner support model | No | For sub-tenant users | Yes | No |
| Anonymous Whistleblower | No | No | No | No | No | No | No | Yes |
| External Certificate Verifier | No | No | No | No | No | No | No | Yes |

---

## Final Notes

1. RegOS already supports a wider persona surface than a pure filing portal: it spans platform operations, regulator supervision, institution operations, partner enablement, and public trust channels.
2. The sharpest role separation today exists in the institution portal for `Admin`, `Maker`, and `Checker`, and in the platform portal for `PlatformAdmin`.
3. The regulator workspace is already rich in stakeholder-specific dashboards, but deeper role segmentation inside that regulator boundary remains a future refinement rather than a hard access split today.
