# Account product and rate display contract

Account statements and annual tax reports accept two additive optional strings
inside each `document` object:

```json
{
  "account_number": "SYNTHETIC-1",
  "account_name": "Min buffert",
  "account_type": "Flex",
  "interest_rate_display": "3,50 %"
}
```

`account_number` retains its existing required-string contract. `account_name`
is the account's display name or customer nickname. `account_type` is the
caller-supplied product display label, for example `Flex` or `Fixed`. Product
labels are not a closed enum: the generator does not maintain banking products.
`interest_rate_display` is the exact, already-formatted rate text to print,
including its percentage unit. It is not a decimal for the generator to convert
or a rate from which it calculates interest.

Missing or empty new fields omit the corresponding metadata. Existing payloads
remain valid and preserve their existing account heading/name behavior. If a
new field is present with null, number, boolean, object or array type, generation
fails with `ExpectedString` at that schema field and zero delivery callbacks.
Both parsers implement the same rules. Diagnostics never include supplied
product names, rate text or other field values.

Statements use the product label as their section heading when available and
show a distinct nickname under `Kontonamn`. Without a product label, the
existing account name (or `Sparkonto`) remains the heading. The account number,
currency and optional `Räntesats` follow. Tax reports show optional `Kontotyp`
and `Räntesats` beside their existing account number/name metadata. All metadata
uses the selected report profile and the existing measured wrapping/pagination.

## Caller ownership

The caller selects the applicable product and rate for the document's snapshot
or reporting period. The native generator performs no database lookup, product
inference, rate lookup, interest calculation, or current/historical-rate
selection. Never substitute a current rate into an old report merely because
it is available. When rates vary over a period, the caller must define the
intended presentation; omit this field until that meaning is agreed.

The current .NET mappers do not yet send these new fields. This is generator-side
support only; existing .NET reports continue to omit them. Host adoption should
supply snapshot-appropriate values. No host or database code changes are part
of this fix. The C ABI layouts and numeric statuses remain unchanged.

`docs/account_metadata_sample.json` is an explicitly synthetic illustration
using fictional display metadata. Generate both sample reports with:

```sh
./build/debug/pdf_generator -i docs/account_metadata_sample.json -o build/account-metadata-preview --profile light-blue
```

`nordiska_account_metadata_tests` covers both document types, both parsers,
missing/empty and malformed values, nicknames distinct from products, supplied
rate text, all three renderers, all four ABI entry points, atomic rejection,
private-marker diagnostics, and long metadata wrapping.
