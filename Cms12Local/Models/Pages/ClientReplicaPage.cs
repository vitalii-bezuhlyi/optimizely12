using System.ComponentModel.DataAnnotations;
using EPiServer.SpecializedProperties;
using EPiServer.Web;

namespace Cms12Local.Models.Pages;

/// <summary>
/// A custom page type that mirrors a client catalog product (e.g. JCB 3CX).
/// Built exactly like ProductPage/ArticlePage (inherits StandardPage), so it
/// inherits the working SEO/MetaData fields, MainBody and MainContentArea,
/// and is rendered by DefaultPageController via ~/Views/ClientReplicaPage/Index.cshtml.
///
/// On top of that it adds many localizable fields of different types, spread
/// across custom tabs, to exercise a translation integration.
///
/// Localization rule in Optimizely:
///   [CultureSpecific]  -> value is per-language (translatable)
///   (no attribute)     -> value is shared/invariant across all language branches
/// </summary>
[SiteContentType(
    GUID = "C7E9A1B2-90AB-4CDE-8123-AABBCCDD0001",
    GroupName = Globals.GroupNames.Products)]
[SiteImageUrl(Globals.StaticGraphicsFolderPath + "page-type-thumbnail-product.png")]
public class ClientReplicaPage : StandardPage
{
    // ---------------------------------------------------------------
    // Core Product Information tab
    // ---------------------------------------------------------------

    [CultureSpecific]
    [Display(Name = "Product name", GroupName = Globals.GroupNames.CoreProductInformation, Order = 10)]
    public virtual string ProductName { get; set; }

    // Invariant - product code / SKU is the same in every language
    [Display(Name = "Product code (shared)", GroupName = Globals.GroupNames.CoreProductInformation, Order = 20)]
    public virtual string ProductCode { get; set; }

    // Localizable list of strings (PropertyStringList) - like bullet USPs
    [CultureSpecific]
    [BackingType(typeof(PropertyStringList))]
    [Display(Name = "Key features", GroupName = Globals.GroupNames.CoreProductInformation, Order = 30)]
    public virtual IList<string> KeyFeatures { get; set; }

    // Invariant numeric spec - same value across languages
    [Display(Name = "Max engine power (kW, shared)", GroupName = Globals.GroupNames.CoreProductInformation, Order = 40)]
    public virtual double? MaxEnginePowerKw { get; set; }

    // Invariant image reference - shared media
    [UIHint(UIHint.Image)]
    [Display(Name = "Feature image (shared)", GroupName = Globals.GroupNames.CoreProductInformation, Order = 50)]
    public virtual ContentReference FeatureImage { get; set; }

    // ---------------------------------------------------------------
    // Product Listing tab
    // ---------------------------------------------------------------

    [CultureSpecific]
    [Display(Name = "Listing title", GroupName = Globals.GroupNames.ProductListing, Order = 10)]
    public virtual string ListingTitle { get; set; }

    [CultureSpecific]
    [Display(Name = "Listing CTA label", GroupName = Globals.GroupNames.ProductListing, Order = 20)]
    public virtual string ListingCtaLabel { get; set; }

    // Localizable boolean - flags can differ per market/language
    [CultureSpecific]
    [Display(Name = "Show quote request", GroupName = Globals.GroupNames.ProductListing, Order = 30)]
    public virtual bool ShowQuoteRequest { get; set; }

    // ---------------------------------------------------------------
    // Commerce Sticky tab
    // ---------------------------------------------------------------

    [CultureSpecific]
    [Display(Name = "Sticky CTA text", GroupName = Globals.GroupNames.CommerceSticky, Order = 10)]
    public virtual string StickyCtaText { get; set; }

    // Localizable link collection (each link's display Text is translatable)
    [CultureSpecific]
    [Display(Name = "Sticky links", GroupName = Globals.GroupNames.CommerceSticky, Order = 20)]
    public virtual LinkItemCollection StickyLinks { get; set; }
}
