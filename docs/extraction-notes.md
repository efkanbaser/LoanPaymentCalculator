# Extraction notes

The updated `ItfaPlanHesapla` method is the calculation source. Its arithmetic, grace-period control flow, output rounding and final repayment behavior were retained. Only the independent calculation was extracted, not the surrounding database writes or application workflow.

Adaptations:

- Company enum description calls became the existing Turkish period labels.
- The payment row model and decimal integer-power helper are local.
- Holiday records come from caller-supplied dates instead of database access. Supplied holidays are not restricted to nominal maturity, because weekend shifting can go beyond it.
- Required/positive input guards prevent the standalone method from dividing by zero or dereferencing missing required values. The typed web boundary adds range and interval-alignment validation.
- The original source's nullable date helper now returns a non-null date for its validated period inputs; this removes migration warnings without changing valid schedule dates.
- A typed adapter parses the source TSV. It does not independently recalculate repayments.

The previous public `app.cs` uses older grace-period behavior. It remains a historical reference; the new demo is not claimed to be identical to that older revision. The updated source's initial grace accrual step is explicitly documented and covered by a characterization check rather than silently corrected.

No company configuration, private package, database schema, customer record, internal endpoint or repository history is included.
