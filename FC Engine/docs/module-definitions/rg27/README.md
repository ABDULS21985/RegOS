# RG-27 Expansion Drafts

This folder contains first-pass multi-jurisdiction module design drafts for:

- Ghana (Bank of Ghana): `ghana_bog_prudential_draft.json`
- Kenya (Central Bank of Kenya): `kenya_cbk_prudential_draft.json`

These draft definitions follow the RG-07 JSON import shape at a high level and capture:

- module identity and jurisdiction code
- core prudential worksheet structure
- key formula expectations
- baseline cross-sheet reconciliation rules
- AML flow hooks to cross-cutting NFIU aggregation

Before production import, each draft still requires full field-level completion, regulator-specific threshold values, and end-to-end validation test fixtures.
