# RegOS Platform Demo Runbook

## Purpose

This runbook is the operator guide for a client-facing RegOS walkthrough on the local demo stack.
It is written against the data currently seeded in SQL Server on `2026-03-27`.

Use this together with [DEMO_CREDENTIALS.md](/Users/mac/codes/fcs/DEMO_CREDENTIALS.md).

## URLs

- Admin portal: `http://localhost:5200/login`
- Institution portal: `http://localhost:5300/login`
- API health: `http://localhost:5100/health`

## Live Demo Estate

### Institution cohorts

- BDC institutions: `80`
- DMB institutions: `28`
- Finance Company institutions: `120`
- Microfinance Bank institutions: `24`

### Template-backed submission footprint

- BDC core filings: `2,880`
- DMB core filings: `1,260`
- Finance Company core filings: `36,000` seeded template submissions
- Finance Company live total: `36,380` because some shared overlay returns already existed on `FCD*`
- MFB core filings: `864`
- Overlay module baseline: `2,220` seeded submissions across `NFIU`, `CAPITAL`, `MODEL`, and `OPS`
- Overlay live total on the representative cohort: `2,237`

### Regulator-side datasets

- Surveillance alerts: `15`
- Stress test runs: `3`
- Policy scenarios: `4`
- Consultation rounds: `1`
- Whistleblower reports: `3`
- Examination projects: `1`

## Best Demo Accounts

### Platform and regulator

- `admin` for platform control-plane walkthroughs
- `cbnadmin` for regulator command-centre walkthroughs
- `cbnapprover` when you want to show MFA-backed regulator access

### Institution portal

- `accessdemo` for the strongest maker-checker-approver story
- `accessmaker`, `accesschecker`, `accessapprover` if you want explicit role switching and MFA
- `bdc001admin` for BDC breadth
- `fcd001admin` for Finance Company breadth
- `mfb001admin` for the cleanest combined MFB plus overlay-module story

## Recommended Story Order

### 1. Platform control plane

Log in with `admin` at the admin portal and use:

- `/dashboard/platform`
- `/platform/tenants`
- `/modules`
- `/platform/module-analytics`
- `/platform/health`

Talking points:

- RegOS is multi-tenant and licence-aware.
- Modules, tenants, templates, billing, and operational health all live in one control plane.
- The seeded estate is large enough to show sector breadth, not just a single sample institution.

### 2. Regulator command centre

Log in with `cbnadmin` and move through:

- `/regulator`
- `/regulator/inbox`
- `/regulator/warnings`
- `/regulator/surveillance`
- `/regulator/stress-test`
- `/regulator/policies`
- `/scenarios/macro`
- `/regulator/systemic-risk`

Talking points:

- The regulator workspace is seeded with filings, alerts, scenarios, and review inventory.
- You can move from submission triage to supervisory analytics without switching products.
- The stress-testing, policy, surveillance, and macro-prudential pages now have usable backing data.

### 3. Institution reporting workflow

Use `accessdemo` first if you want the cleanest human workflow story:

- `/dashboard/admin`
- `/submissions`
- `/submit`
- `/validation/hub`
- `/approvals`
- `/reports`
- `/institution/team`

Then switch to role accounts if you want to show control segregation:

- `accessmaker`
- `accesschecker`
- `accessapprover`

Talking points:

- Maker-checker-approver separation is demonstrable.
- Validation, submission history, approvals, reports, and team administration are all live.
- Access Bank is the best institution for role-driven storytelling.

### 4. Sector breadth

Use one institution per sector to prove the platform is not single-pack:

- BDC: `bdc001admin`
- DMB: `dmb001admin` or `accessdemo`
- FC: `fcd001admin`
- MFB: `mfb001admin`

Useful routes:

- `/dashboard/module/{moduleCode}`
- `/templates`
- `/submissions`
- `/reports`
- `/calendar`

Suggested module codes:

- BDC: `BDC_CBN`
- DMB: `DMB_BASEL3`
- FC: `FC_RETURNS`
- MFB: `MFB_PAR`

## Overlay Module Demo

### Best tenant for a combined overlay story

Use `mfb001admin`.

Why:

- It has its core `MFB_PAR` pack plus all four overlay families.
- Its overlay counts are clean and uniform.
- It avoids the small amount of older NFIU history present on some BDC, DMB, and FC representative tenants.

### Overlay capability families

- AML / financial intelligence: `NFIU_AML`
- Capital supervision: `CAPITAL_SUPERVISION`
- Model risk: `MODEL_RISK`
- Operational resilience: `OPS_RESILIENCE`

Institution routes:

- `/modules`
- `/dashboard/module/NFIU_AML`
- `/dashboard/module/CAPITAL_SUPERVISION`
- `/dashboard/module/MODEL_RISK`
- `/dashboard/module/OPS_RESILIENCE`
- `/workflows/{ModuleKey}`

Regulator follow-through routes:

- `/platform/capital`
- `/platform/capital/planning`
- `/platform/capital/buffers`
- `/platform/capital/rwa`
- `/regulator/models`
- `/regulator/models/inventory`
- `/regulator/models/validation`
- `/regulator/resilience`
- `/regulator/resilience/services`
- `/regulator/resilience/testing`

## Public and trust flows

Use these when you want to end the demo on trust and transparency rather than only filings:

- Whistleblower intake: `/regulator/whistleblower/report`
- Whistleblower case management: `/regulator/whistleblower/cases`
- Certificate verification: `/verify/{CertId}`

Talking points:

- RegOS covers public trust workflows, not just regulated-entity submissions.
- The same platform supports supervisory investigation, case handling, and public verification patterns.

## Fast Fallback Screens

If a detailed workflow is taking too long, these are the safest high-signal pages:

- Platform posture: `/dashboard/platform`
- Regulator command centre: `/regulator`
- Regulator inbox: `/regulator/inbox`
- Stress testing: `/regulator/stress-test`
- Policy simulation: `/regulator/policies`
- Macro-prudential workspace: `/scenarios/macro`
- Institution admin dashboard: `/dashboard/admin`
- Submission history: `/submissions`
- Validation workspace: `/validation/hub`

## Suggested Demo Narrative

1. Start with `admin` to establish multi-tenant control, modules, and scale.
2. Move to `cbnadmin` to show supervisory command, alerts, policy, stress, and macro views.
3. Switch to `accessdemo` and role accounts to show filing preparation, review, approval, and reporting.
4. Use `mfb001admin` to show that RegOS also handles AML, capital, model-risk, and resilience overlays on top of a core filing pack.
5. Close with whistleblower or certificate-verification flows.

## Notes

- `BDC001` to `BDC005`, `DMB001` to `DMB005`, `FCD001` to `FCD005`, and `MFB001` to `MFB005` carry the overlay module packs.
- The overlay cohort has `2,220` clean seeded submissions plus `17` older NFIU historical submissions already present on some representative BDC, DMB, and FC tenants.
- `MFB001` is the cleanest single institution when you want one tenant to tell a broad story without historical noise.
- The credential pack is the source of truth for usernames, passwords, MFA secrets, and backup codes.
