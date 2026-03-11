# RegOS Backlog Catalog

Catalog of backlog themes and candidate modules discussed during the product planning session on 2026-03-11.

## Status Key

- `Planned` = agreed as a backlog item
- `Priority Now` = recommended for earlier implementation
- `Deferred` = useful, but recommended after prerequisite work

## Portfolio Summary

| Theme | Status | Recommendation |
|---|---|---|
| Capital Management & Supervisory Planning | Planned | Priority Now |
| Regulatory Knowledge Graph & Compliance Navigation | Planned | Priority Now |
| AML / TFS Screening & Regulatory Reporting Engine | Planned | Priority Now |
| Operational Resilience & ICT Risk | Planned | Priority Next |
| Model Risk Management & Validation Framework | Planned | Priority Next |
| Stakeholder Dashboards | Planned | Return later after core analytics modules mature |
| Capital Stack Optimiser | Deferred | Build after capital analytics foundation |

## 1. Capital Management & Supervisory Planning

### Scope

- RWA optimisation
- Buffer management
- Capital planning and forecasting
- What-if capital actions

### Decision Notes

- Treat items `2`, `3`, `4`, and `5` from the original capital concept as the planned capital-management track.
- Keep `Capital Stack Optimiser` in backlog, but defer it until the broader capital analytics layer is in place.

### Why It Fits RegOS

- Extends existing capital, stress-testing, and early-warning capabilities
- Useful for both regulator oversight and institution planning
- Produces actionable capital and supervisory intelligence, not just visual reporting

### Candidate Capabilities

| Capability | Outcome |
|---|---|
| RWA Optimisation | Identify collateral, exposure mix, and portfolio actions that improve CAR without changing strategy |
| Buffer Management | Track minimum capital, conservation, countercyclical, and systemic buffers with trigger warnings |
| Capital Planning & Forecasting | Project CAR over 4 to 8 quarters using growth, profit, dividend, and issuance assumptions |
| What-If Capital Actions | Compare issuance, disposal, and portfolio action scenarios side by side |

### Recommended Sequencing

1. RWA optimisation
2. Buffer management
3. Capital planning and forecasting
4. What-if capital actions
5. Capital stack optimiser

### Deferred Item

`Capital Stack Optimiser` remains valuable, but should come after the four items above because it requires a deeper optimization layer for CET1, AT1, and Tier 2 instrument structuring.

## 2. Regulatory Knowledge Graph & Compliance Navigation

### Scope

- Regulatory ontology
- SQL-first knowledge graph store
- Compliance navigator UI
- Per-institution obligation register
- Regulatory change impact propagation

### Target Scale

- 6 regulators
- 200+ regulations
- 2,000+ requirements
- ~14 modules
- ~175 templates
- ~3,500 fields

### Decision Notes

- Start SQL-first, not Neo4j-first.
- Make `Obligation Register` and `Impact Propagation` the first visible outcomes.
- Build this before major dashboard expansion.

### Why It Fits RegOS

- Builds on existing field-level regulatory references and template metadata
- Creates a platform-wide lineage layer from regulation to obligation to template to field to institution
- Strengthens explainability, compliance navigation, and change impact analysis

### Candidate Capabilities

| Capability | Outcome |
|---|---|
| Regulatory Ontology | Structured model of regulators, regulations, sections, obligations, deadlines, penalties, exemptions, and links |
| Knowledge Graph Store | SQL-based graph tables and materialized edges across regulators, modules, templates, and fields |
| Compliance Navigator UI | Trace regulation -> template -> field -> institution, and reverse from field back to source regulation |
| Obligation Register | Show each institution its active obligations based on licence types and module applicability |
| Impact Propagation | Trace regulatory changes through templates, fields, formulas, and affected institutions |

### Recommended Sequencing

1. Obligation register
2. Impact propagation
3. Compliance navigator UI
4. Full ontology expansion
5. Advanced graph services

### Architecture Recommendation

Start SQL-first rather than introducing Neo4j in v1. The existing platform is already centered on relational metadata and the first delivery phase depends more on curated lineage than graph-specific infrastructure.

### Suggested Entity Set

- `Regulator`
- `Regulation`
- `Circular`
- `Guideline`
- `Section`
- `Requirement`
- `Obligation`
- `DeadlineRule`
- `Penalty`
- `Exemption`
- `RequirementAppliesToLicenceType`
- `RequirementImplementedByTemplate`
- `RequirementCapturedByField`
- `kg_nodes`
- `kg_edges`

### Success Outcomes

1. Knowledge graph contains 6 regulators, 200+ regulations, and 2,000+ requirements.
2. Every template field links to its regulatory source.
3. Compliance navigator traces regulation -> template -> field.
4. Obligation register shows obligations per institution.
5. Regulatory change impact propagates through the graph.

## 3. AML / TFS Screening & Regulatory Reporting Engine

### Scope

- Watchlist ingestion and updates
- Fuzzy screening engine
- Alert workflow and audit trail
- False-positive library
- NFIU TFS auto-population
- STR auto-drafting with goAML linkage

### Watchlist Sources

- UN Security Council Consolidated List
- OFAC SDN List
- EU Consolidated List
- UK HMT Sanctions List
- EFCC / NFIU domestic lists
- Interpol Red Notices
- PEP databases

### Decision Notes

- This is a new sanctions-screening capability, not an extension of the existing compliance-health watchlist endpoint.
- Best product framing is `AML / TFS Screening & Regulatory Reporting Engine`, not just `sanctions screening`.

### Why It Fits RegOS

- Converts RegOS from a reporting platform into an operational AML / TFS workflow engine
- Aligns directly with NFIU reporting and goAML submission paths
- Creates reusable intelligence for compliance, surveillance, and supervisory review

### Candidate Capabilities

| Capability | Outcome |
|---|---|
| Watchlist Integration | Daily ingestion of global and domestic sanctions and watchlist data |
| Screening Engine | Batch and real-time fuzzy matching across customers, counterparties, and transactions |
| Alert Management | Review workflow for true match, potential match, and false positive decisions |
| False Positive Library | Prevent re-alerting on previously dismissed recurring matches |
| TFS Compliance Integration | Auto-populate `NFIU_TFS` screening and match metrics |
| STR Auto-Drafting | Draft STRs for compliance review when confirmed sanctioned-entity matches are found |

### Matching Methods

- Levenshtein
- Soundex
- Jaro-Winkler
- Configurable screening thresholds by risk profile

### Recommended Sequencing

1. Watchlist ingestion and normalization
2. Screening engine
3. Alert workflow and false-positive library
4. NFIU TFS auto-population
5. STR auto-drafting
6. Real-time transaction screening API

### Success Outcomes

1. All 6+ watchlists loaded and auto-updated daily.
2. Fuzzy matching identifies name variants.
3. Alert workflow tracks review decisions with audit.
4. False-positive library prevents re-alerting.
5. `NFIU_TFS` return auto-populates from screening data.
6. STR is auto-drafted on confirmed match.

## 4. Operational Resilience & ICT Risk

### Scope

- New `OPS_RESILIENCE` module
- 10 return sheets
- Self-assessment and gap analysis
- Third-party concentration risk analysis
- Incident lifecycle timeline
- Resilience testing schedule and results
- Board resilience dashboard

### Positioning

Inspired by EU DORA and CBN operational risk expectations, but framed for RegOS as `Operational Resilience & ICT Risk` rather than a literal DORA clone.

### Proposed Return Sheets

1. Important Business Services Inventory
2. Impact Tolerance Definitions
3. Scenario Testing Results
4. ICT Third-Party Risk Register
5. Incident Management Reporting
6. Business Continuity Plan Status
7. Cyber Resilience Assessment
8. Change Management Controls
9. Recovery Time Testing
10. Board Resilience Oversight

### Why It Fits RegOS

- Uses the existing module-pack and return-template pattern
- Can build on current cyber asset, dependency, incident, root-cause, and resilience scenario structures
- Serves both supervisory oversight and institutional control functions

### Candidate Capabilities

| Capability | Outcome |
|---|---|
| Self-Assessment Questionnaire | Gap score per important business service |
| Third-Party Concentration Analysis | Identify critical-service dependency concentration on shared providers |
| Incident Timeline Builder | Capture detection, escalation, containment, recovery, and remediation lifecycle |
| Testing Schedule Tracker | Track planned resilience exercises and their outcomes |
| Board Dashboard | Provide executive-level view of resilience posture and unresolved critical issues |

### Core Entity Candidates

- `ImportantBusinessService`
- `ImpactTolerance`
- `ThirdPartyProvider`
- `ServiceDependency`
- `OperationalIncident`
- `ResilienceTestRun`
- `BoardOversightRecord`

### Recommended Sequencing

1. `OPS_RESILIENCE` module and sheets
2. Important business service and impact tolerance entities
3. Third-party provider and dependency model
4. Incident and testing workflow
5. Board and regulator dashboards

### Success Outcomes

1. Operational resilience module loaded with 10 return sheets.
2. Self-assessment questionnaire computes gap score.
3. Third-party concentration risk is identified.
4. Incident timeline captures full lifecycle.
5. Resilience testing schedule is tracked with results.

## 5. RG-50 Model Risk Management & Validation Framework

### Prompt Metadata

| Field | Value |
|---|---|
| Prompt ID | RG-50 |
| Stream | I — Platform Intelligence & Marketplace |
| Phase | Phase 5 |
| Principle | Transform RegOS into a leading Regulatory & SupTech platform |
| Depends On | RG-09, RG-32, AI-01 |
| Estimated Effort | 5–7 days |

### Scope

- Central model inventory
- Validation framework and schedule
- Ongoing model performance monitoring
- Model change management workflow
- Regulatory reporting on model risk

### Decision Notes

- Build this as a supervisory model-governance capability, not a generic enterprise model catalog.
- Keep v1 focused on regulatory and prudential models only.

### Why It Fits RegOS

- Extends existing validation, workflow, audit, scenario, and model-calculation capabilities
- Supports supervisory governance over ECL, capital, liquidity, stress, and climate models
- Creates a structured governance layer around the models already embedded in regulatory processes

### Candidate Capabilities

| Capability | Outcome |
|---|---|
| Model Inventory | Register all regulatory models with owner, purpose, tier, materiality, and validation metadata |
| Validation Framework | Track conceptual review, data integrity checks, implementation testing, backtesting, and sensitivity analysis |
| Performance Monitoring | Monitor PSI, Gini, calibration drift, predicted versus actual outcomes, and degradation thresholds |
| Change Management | Require rationale, impact assessment, parallel runs, and approval chain for model changes |
| Model Risk Reporting | Generate model inventory, validation, change, and risk-appetite summary returns |

### Recommended Sequencing

1. Model inventory
2. Validation scheduling and report templates
3. Performance monitoring metrics store
4. Change management workflow
5. Model risk reporting sheets

### Suggested v1 Boundary

Limit v1 to regulatory and prudential models:

- IFRS 9 / ECL
- PD / LGD / EAD
- CAR / LCR / NSFR
- Stress testing
- Climate risk models

### Success Outcomes

1. Model inventory lists regulatory models with metadata.
2. Validation framework schedules and tracks validations.
3. Backtesting compares predicted versus actual outcomes.
4. Model performance monitoring alerts on degradation.
5. Change management workflow tracks approvals.
6. Model risk return sheets generate correctly.

## 6. Stakeholder Dashboards

### Scope

- Governor dashboard
- Deputy Governor dashboard
- Director / examiner dashboard
- Institution executive dashboard

### Why It Fits RegOS

- Creates decision-grade views over the intelligence produced by the modules above
- Useful after capital, resilience, knowledge graph, and screening capabilities are in place
- Best treated as a presentation layer over deeper platform analytics

### Candidate Dashboard Types

| Stakeholder | Focus |
|---|---|
| Governor | Systemic posture, sector risk, compliance trend, emerging threats, strategic escalations |
| Deputy Governor | Portfolio-specific oversight, unresolved issues, risk concentrations, module health |
| Director / Examiner | Operational queues, overdue reviews, validation failures, drilldowns, remediation tracking |
| Institution Executive | Filing posture, rejected returns, upcoming deadlines, peer benchmarks, unresolved regulator queries |

### Recommendation

Return to dashboard expansion after the higher-value data and intelligence layers above are in place.

### Candidate Sequence When Revisited

1. Governor dashboard
2. Deputy Governor dashboard
3. Director / examiner dashboard
4. Institution executive dashboard

## Backlog Sequencing Recommendation

### Wave 1

1. Regulatory Knowledge Graph
2. Capital Management & Supervisory Planning
3. AML / TFS Screening & Regulatory Reporting Engine

### Wave 2

1. Operational Resilience & ICT Risk
2. RG-50 Model Risk Management & Validation Framework

### Wave 3

1. Stakeholder Dashboards
2. Capital Stack Optimiser

## Notes

- The backlog above reflects the product conversation only. It is not a delivery commitment or a release plan.
- Several items build naturally on current RegOS foundations, but each still requires formal data modeling, workflow design, and implementation planning.
