# Dive Hub branding and public defaults

The bundled logo is the existing Dive Hub İskele mark from the [official website](https://dive-hub.com/en/). Its source asset is [logo2-01.png](https://dive-hub.com/wp-content/uploads/2015/07/logo2-01.png), downloaded on 13 September 2026 and embedded unchanged as `Resources/Brand/dive-hub-logo.png` for this Dive Hub application. The mark remains its owner's property.

The following colors were sampled directly from opaque pixels in that asset:

| Resource | Value | Source |
|---|---|---|
| `DiveHubNavy` | `#0D2F4F` | Logo shield fill |
| `DiveHubBlue` | `#2D6E8E` | Logo anchor fill |
| `DiveHubAccent` | `#20B5BD` | Complementary UI accent; not sampled |

Surface, background and text colors are supporting application colors. Replace named values in `Resources/Colors.xaml` to adjust the palette. Typography and native control styles are separate dictionaries.

Optional dive-site names come from the public [Dive Hub dive-sites page](https://dive-hub.com/en/services/dive-sites/). No private staff/customer details or copied long site descriptions are bundled. Coordinates and operating depths are left blank for the center to enter. Activities and site defaults can be edited/deactivated or omitted entirely.

The application uses WPF native controls and open-source Microsoft/SQLite packages, without paid UI libraries. EF Core and Microsoft extensions are pinned to 10.0.8; `SQLitePCLRaw.bundle_e_sqlite3` is explicitly pinned to 3.0.5 to avoid the vulnerable older transitive native bundle reported during restore. See the [bundle package](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5) and [the reported advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
