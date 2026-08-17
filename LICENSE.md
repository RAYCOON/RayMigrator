# RayMigrator License — Business Source License 1.1 with Additional Use Grant

Business Source License 1.1

License text copyright © 2017 MariaDB Corporation Ab, All Rights Reserved.
"Business Source License" is a trademark of MariaDB Corporation Ab.

---

## Parameters

| Field                | Value                                                              |
|----------------------|--------------------------------------------------------------------|
| Licensor             | RAYCOON.com GmbH, Mainzer Str. 16g, 64331 Weiterstadt, Germany     |
| Licensed Work        | RayMigrator 0.11.0 — © 2026 RAYCOON.com GmbH                       |
| Additional Use Grant | See section below                                                  |
| Change Date          | Four (4) years after the first publicly available distribution of each version of the Licensed Work (per-version; see License Text below) |
| Change License       | Apache License, Version 2.0 (https://www.apache.org/licenses/LICENSE-2.0) [^1] |

[^1]: Apache License 2.0 is GPLv3-compatible. Under BSL 1.1 Covenant 1
    ("compatible with GPL Version 2.0 or a later version"), GPLv3
    compatibility satisfies the covenant — consistent with the
    interpretation adopted by other BSL 1.1 adopters (e.g., CockroachDB).

For information about alternative licensing arrangements for the Licensed Work,
please contact `raymigrator@raycoon.com`.

---

## Additional Use Grant

You may make production use of the Licensed Work free of charge, for any
purpose, without restriction as to organization size, legal form, sector, or
field of endeavour. This includes making the Licensed Work available to third
parties as part of a hosted, SaaS, or managed service offering.

---

## Supplemental Terms

### Precedence

These Supplemental Terms **supplement**, but do not modify, the BSL 1.1
License Text below. To the extent mandatory provisions of German law render
any clause of the BSL 1.1 License Text inapplicable, ineffective, or
otherwise modified as applied to licensees domiciled in or governed by the
laws of the Federal Republic of Germany, these Supplemental Terms govern the
resulting legal framework. The BSL 1.1 License Text itself remains unmodified
in compliance with BSL 1.1 Covenant 4.

### Precedence over Accompanying Materials

This License is the sole and exclusive statement of the terms on which the
Licensed Work is made available. Statements about the Licensed Work in any
other material — including the project website, `README` files, package
metadata, changelogs, technical documentation, release notes, issue trackers,
presentations, and marketing communications — are informative only. They do
not form part of this License, do not modify it, and do not constitute a
guarantee or an agreement on the quality or characteristics of the Licensed
Work. Section 1(d) of the Liability section below remains unaffected.

In case of any conflict between this License and any such material, this
License prevails.

### Definitions

These definitions apply throughout this License:

- *Production Use*: any use of the Licensed Work in a live, operational,
  or revenue-generating environment.
- *Software*: where used in any communication or documentation referring
  to this License, equivalent to the BSL term *Licensed Work*.

### Scope of the Licensed Work

**Database.Example Carve-Out.** The contents of the directory
`Raycoon.RayMigrator.Database.Example` (including its skeleton DAL plugin
template, placeholder SQL templates, and project file) are licensed under the
**MIT License** (see the `LICENSE.md` file inside that directory). They are not
part of the Licensed Work; neither the Additional Use Grant nor the Business
Source License 1.1 applies to the contents of that directory.

### Trademark Reservation

The names "RayMigrator" and "RAYCOON", together with all associated logos,
word marks, and visual identities (collectively the "Marks"), are claimed as
unregistered trademarks of RAYCOON.com GmbH.

Nothing in this License — including the BSL grant of redistribution rights —
grants any right to use the Marks.

Verbatim redistribution of unmodified copies of the Licensed Work under its
original name, package identifiers, binary names, configuration keys, and CLI
command names is expressly permitted and is not restricted by this section.

In particular, modified or derivative versions of the Licensed Work:

1. must not be distributed under the name "RayMigrator" or any confusingly
   similar name;
2. must not bear the Marks in product names, package identifiers, binary
   names, configuration keys (e.g., a section named "RayMigrator" in
   `appsettings.json`), CLI command names, or marketing materials, except
   for unmodified attribution notices required by this License;
3. must clearly identify themselves as a fork or modified version under a
   distinct name not derived from the Marks.

Use of the Marks for nominative reference (e.g., "compatible with
RayMigrator", "based on RayMigrator") is permitted only to the extent
necessary for accurate technical description and must not suggest
endorsement by RAYCOON.com GmbH.

### Governing Law and Jurisdiction

These Supplemental Terms and the Additional Use Grant are governed by the
laws of the Federal Republic of Germany. Place of jurisdiction for disputes
arising from or in connection with these terms is Darmstadt, Germany, to the
extent permitted by law.

### Liability

#### 1. Unlimited liability

The Licensor's liability is unlimited:

(a) for damages to life, body, or health resulting from a negligent or
    intentional breach of duty by the Licensor or its legal representatives
    or vicarious agents;

(b) for damages caused by intent or gross negligence by the Licensor or its
    legal representatives or vicarious agents;

(c) for claims arising under the German Product Liability Act
    (Produkthaftungsgesetz);

(d) for damages resulting from the fraudulent concealment of defects or
    from the breach of an express guarantee given by the Licensor.

#### 2. Limited liability for breach of cardinal duties

For damages caused by ordinary negligence in the breach of a cardinal duty
(Kardinalpflicht — i.e., a duty whose fulfilment is essential to the proper
performance of this License and on which the Licensee may regularly rely),
the Licensor's liability is limited to damages that are typical and
foreseeable for the licensing of database migration software at the time
the Licensed Work is made available.

#### 3. Exclusion of further liability

Beyond the cases set out in sections 1 and 2 above, the Licensor's liability
for ordinary negligence is excluded.

#### 4. Liability cap and data-loss limitation

(a) Liability under section 2 is capped at **EUR 10,000** per calendar year.

(b) Liability for loss of data is limited to the cost of restoring the
    data from a backup that the Licensee maintains, or would have
    maintained, in accordance with state-of-the-art data backup practices
    appropriate to the criticality of the affected systems.

#### 5. Backup obligation of the Licensee

The Licensee acknowledges that database migrations carry an inherent risk
of data loss and undertakes to maintain a current, restorable backup of all
data affected by any operation performed with the Licensed Work prior to
running such operation. Where a failure to maintain such backup has
contributed to a damage, the statutory rules on contributory fault (§ 254
of the German Civil Code, BGB) apply.

#### 6. Limitation period

Claims for damages against the Licensor become time-barred after twelve
(12) months from the date the Licensee became aware, or should have become
aware without gross negligence, of the damage and the person liable, except
in the cases of section 1, which remain subject to the statutory limitation
period.

#### 7. Consumers

Where the Licensee is a consumer within the meaning of § 13 of the German
Civil Code (BGB), the statutory rules on liability and on limitation periods
apply in place of sections 4, 5 and 6 above.

### Contractual Penalty

For each culpable breach of the Trademark Reservation above, or of the
obligation under the BSL 1.1 License Text to conspicuously display this
License on each original or modified copy, the Licensee shall pay a
contractual penalty to the Licensor.

The amount of the penalty is determined by the Licensor at its reasonable
discretion (§ 315 of the German Civil Code, BGB), taking into account the
nature, duration, and severity of the breach, the degree of fault, and any
commercial benefit derived from it. Its appropriateness is subject to review
by the competent court. A continuing breach counts as a single breach.

Any contractual penalty paid is credited against a claim for damages arising
from the same breach (§ 341 (2) BGB).

This section does not apply where the Licensee is a consumer within the
meaning of § 13 BGB.

### Termination Details

Upon termination of rights under the BSL License Text or these Supplemental
Terms, all Production Use must cease immediately and all copies of the
Licensed Work in the Licensee's possession or control must be deleted,
except where retention is required by mandatory law.

---

## License Text

**Terms**

The Licensor hereby grants you the right to copy, modify, create derivative
works, redistribute, and make non-production use of the Licensed Work. The
Licensor may make an Additional Use Grant, above, permitting limited
production use.

Effective on the Change Date, or the fourth anniversary of the first publicly
available distribution of a specific version of the Licensed Work under this
License, whichever comes first, the Licensor hereby grants you rights under the
terms of the Change License, and the rights granted in the paragraph above
terminate.

If your use of the Licensed Work does not comply with the requirements
currently in effect as described in this License, you must purchase a
commercial license from the Licensor, its affiliated entities, or authorized
resellers, or you must refrain from using the Licensed Work.

All copies of the original and modified Licensed Work, and derivative works of
the Licensed Work, are subject to this License. This License applies separately
for each version of the Licensed Work and the Change Date may vary for each
version of the Licensed Work released by Licensor.

You must conspicuously display this License on each original or modified copy
of the Licensed Work. If you receive the Licensed Work in original or modified
form from a third party, the terms and conditions set forth in this License
apply to your use of that work.

Any use of the Licensed Work in violation of this License will automatically
terminate your rights under this License for the current and all other versions
of the Licensed Work.

This License does not grant you any right in any trademark or logo of Licensor
or its affiliates (provided that you may use a trademark or logo of Licensor as
expressly required by this License).

TO THE EXTENT PERMITTED BY APPLICABLE LAW, THE LICENSED WORK IS PROVIDED ON AN
"AS IS" BASIS. LICENSOR HEREBY DISCLAIMS ALL WARRANTIES AND CONDITIONS, EXPRESS
OR IMPLIED, INCLUDING (WITHOUT LIMITATION) WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE, NON-INFRINGEMENT, AND TITLE.

MariaDB hereby grants you permission to use this License's text to license your
works, and to refer to it using the trademark "Business Source License", as
long as you comply with the Covenants of Licensor below.

**Covenants of Licensor**

In consideration of the right to use this License's text and the "Business
Source License" name and trademark, Licensor covenants to MariaDB, and to all
other recipients of the licensed work to be provided by Licensor:

1. To specify as the Change License the GPL Version 2.0 or any later version,
   or a license that is compatible with GPL Version 2.0 or a later version,
   where "compatible" means that software provided under the Change License
   can be included in a program with software provided under GPL Version 2.0
   or a later version. Licensor may specify additional Change Licenses without
   limitation.

2. To either: (a) specify an additional grant of rights to use that does not
   impose any additional restriction on the right granted in this License, as
   the Additional Use Grant; or (b) insert the text "None".

3. To specify a Change Date.

4. Not to modify this License in any other way.

**Notice**

The Business Source License (this document, or the "License") is not an Open
Source license. However, the Licensed Work will eventually be made available
under an Open Source License, as stated in this License.
