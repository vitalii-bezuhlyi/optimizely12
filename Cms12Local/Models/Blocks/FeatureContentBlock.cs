using System.ComponentModel.DataAnnotations;
using EPiServer.SpecializedProperties;
using EPiServer.Web;

namespace Cms12Local.Models.Blocks;

/// <summary>
/// A content block with several localizable field types.
/// Used inside a page's ContentArea to verify that a translation
/// integration recurses into nested blocks (not only page-level properties).
/// </summary>
[SiteContentType(
    GUID = "B1F2C3D4-1111-2222-3333-444455556666",
    GroupName = SystemTabNames.Content)]
[SiteImageUrl]
public class FeatureContentBlock : SiteBlockData
{
    // Plain localizable string
    [CultureSpecific]
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Heading", GroupName = SystemTabNames.Content, Order = 10)]
    public virtual string Heading { get; set; }

    // Localizable multi-line text
    [CultureSpecific]
    [UIHint(UIHint.Textarea)]
    [Display(Name = "Sub heading", GroupName = SystemTabNames.Content, Order = 20)]
    public virtual string SubHeading { get; set; }

    // Localizable rich-text / HTML (XhtmlString) - like the client's RTE Block
    [CultureSpecific]
    [Display(Name = "Body", GroupName = SystemTabNames.Content, Order = 30)]
    public virtual XhtmlString Body { get; set; }

    // Localizable call-to-action label
    [CultureSpecific]
    [Display(Name = "CTA label", GroupName = SystemTabNames.Content, Order = 40)]
    public virtual string CtaLabel { get; set; }

    // Localizable collection of links (each LinkItem has a translatable Text)
    [CultureSpecific]
    [Display(Name = "Links", GroupName = SystemTabNames.Content, Order = 50)]
    public virtual LinkItemCollection Links { get; set; }

    // NOT localizable - shared across all languages (image is the same everywhere)
    [UIHint(UIHint.Image)]
    [Display(Name = "Image (shared)", GroupName = SystemTabNames.Content, Order = 60)]
    public virtual ContentReference Image { get; set; }
}
