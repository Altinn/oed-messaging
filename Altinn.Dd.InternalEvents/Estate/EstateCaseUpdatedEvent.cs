using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

/// <summary>
/// The eventtype published when updates for an estate are recevied from DA
/// </summary>
/// <remarks>
/// Carried by both <see cref="EventType.CaseStatusUpdateValidated"/> (an update arrived on the
/// hendelsesliste) and <see cref="EventType.CaseStatusManuallySynced"/> (a single case was
/// re-synced by hand).
/// </remarks>
public class EstateCaseUpdatedEvent
{
    /// <summary>When the update was produced.</summary>
    [JsonPropertyName("time")]
    public required DateTimeOffset Time { get; set; }

    /// <summary>The Domstoladministrasjonen case id of the estate.</summary>
    [JsonPropertyName("caseId")]
    public required string CaseId { get; set; }

    /// <summary>The case number as shown to users.</summary>
    [JsonPropertyName("caseNumber")]
    public required string CaseNumber { get; set; }

    /// <summary>The current status of the case at Domstoladministrasjonen.</summary>
    [JsonPropertyName("caseStatus")]
    public required string CaseStatus { get; set; }

    /// <summary>The district court handling the estate.</summary>
    [JsonPropertyName("districtCourtName")]
    public required string DistrictCourtName { get; set; }

    /// <summary>The deadline for the heirs to complete probate.</summary>
    [JsonPropertyName("probateDeadline")]
    public required DateTimeOffset ProbateDeadline { get; set; }

    /// <summary>The probate outcome, once the case has one.</summary>
    [JsonPropertyName("probateResultV2")]
    public ProbateResultV2? ProbateResultV2 { get; set; }

    /// <summary>The kind of result reached, when reported separately from the outcome.</summary>
    [JsonPropertyName("resultType")]
    public string? ResultType { get; set; }

    /// <summary>Whether the case has been cancelled.</summary>
    [JsonPropertyName("isCancelled")]
    public bool? IsCancelled { get; set; }

    /// <summary>When the heirs were granted access to the estate.</summary>
    [JsonPropertyName("accessDate")]
    public DateTimeOffset? AccessDate { get; set; }

    /// <summary>The parties involved in the estate and the role each holds.</summary>
    [JsonPropertyName("heirRolesV2")]
    public IEnumerable<HeirRoleV2> HeirRolesV2 { get; set; } = [];
}

/// <summary>
/// The outcome of probate for an estate.
/// </summary>
public class ProbateResultV2
{
    /// <summary>The heirs the outcome applies to.</summary>
    [JsonPropertyName("heirs")]
    public IEnumerable<ProbateHeir> Heirs { get; set; } = [];

    /// <summary>The outcome reached for the estate.</summary>
    [JsonPropertyName("result")]
    public required string Result { get; set; }
}

/// <summary>
/// The name of a person who has no national identity number to identify them by.
/// </summary>
public class PersonName
{
    /// <summary>The surname. May be empty, but is always present.</summary>
    [JsonPropertyName("lastName")]
    [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
    public required string LastName { get; set; }

    /// <summary>The given name. May be empty, but is always present.</summary>
    [JsonPropertyName("firstName")]
    [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
    public required string FirstName { get; set; }

    /// <summary>Any middle names, in order.</summary>
    [JsonPropertyName("middleName")]
    public ICollection<string> MiddleName { get; set; } = [];
}

/// <summary>
/// A party involved in an estate and the role they hold. Deserialises to one of the derived types
/// according to the <c>type</c> discriminator; the <c>Papp</c> variants describe parties that
/// exist only on paper and so carry a name instead of an identifying number.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PersonHeirRole), typeDiscriminator: "Person")]
[JsonDerivedType(typeof(PappPersonHeirRole), typeDiscriminator: "PappPerson")]
[JsonDerivedType(typeof(OrganizationHeirRole), typeDiscriminator: "Organization")]
[JsonDerivedType(typeof(PappOrganizationHeirRole), typeDiscriminator: "PappOrganization")]
public abstract class HeirRoleV2
{
    /// <summary>The party's relation to the deceased.</summary>
    [JsonPropertyName("relation")]
    public required string Relation { get; set; }

    /// <summary>Whether this party should receive the probate certificate.</summary>
    [JsonPropertyName("probateCertificateRecipient")]
    public bool ProbateCertificateRecipient { get; set; }
}

/// <summary>
/// An organization party that exists only on paper, identified by name.
/// </summary>
public class PappOrganizationHeirRole : HeirRoleV2
{
    /// <summary>The organization number, if one is known.</summary>
    [JsonPropertyName("orgNo")]
    public string? OrgNo { get; set; }

    /// <summary>The country the organization is registered in, if known.</summary>
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    /// <summary>The name of the organization.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

/// <summary>
/// An organization party identified by its organization number.
/// </summary>
public class OrganizationHeirRole : HeirRoleV2
{
    /// <summary>The organization number.</summary>
    [JsonPropertyName("orgNo")]
    public required string OrgNo { get; set; }
}

/// <summary>
/// A person party that exists only on paper, identified by name and date of birth.
/// </summary>
public class PappPersonHeirRole : HeirRoleV2
{
    /// <summary>The person's name.</summary>
    [JsonPropertyName("name")]
    public required PersonName Name { get; set; }

    /// <summary>The person's date of birth.</summary>
    [JsonPropertyName("dateOfBirth")]
    public required DateTimeOffset DateOfBirth { get; set; }
}

/// <summary>
/// A person party identified by their national identity number.
/// </summary>
public class PersonHeirRole : HeirRoleV2
{
    /// <summary>The person's national identity number.</summary>
    [JsonPropertyName("nin")]
    public required string Nin { get; set; }

    /// <summary>The role the person holds in the estate, when one is assigned.</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>When the person signed the declaration, if they have.</summary>
    [JsonPropertyName("signedDate")]
    public DateTimeOffset? SignedDate { get; set; }
}

/// <summary>
/// An heir named in a probate outcome. Deserialises to one of the derived types according to the
/// <c>type</c> discriminator, mirroring <see cref="HeirRoleV2"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PersonProbateHeir), typeDiscriminator: "Person")]
[JsonDerivedType(typeof(PappPersonProbateHeir), typeDiscriminator: "PappPerson")]
[JsonDerivedType(typeof(OrganizationProbateHeir), typeDiscriminator: "Organization")]
[JsonDerivedType(typeof(PappOrganizationProbateHeir), typeDiscriminator: "PappOrganization")]
public abstract class ProbateHeir
{
    /// <summary>Whether the heir is willing to assume the debts of the estate.</summary>
    [JsonPropertyName("willingToAssumeDebt")]
    public bool WillingToAssumeDebt { get; set; } = false;
}

/// <summary>
/// An organization heir that exists only on paper, identified by name.
/// </summary>
public class PappOrganizationProbateHeir : ProbateHeir
{
    /// <summary>The name of the organization.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

/// <summary>
/// An organization heir identified by its organization number.
/// </summary>
public class OrganizationProbateHeir : ProbateHeir
{
    /// <summary>The organization number.</summary>
    [JsonPropertyName("orgNo")]
    public required string OrgNo { get; set; }
}

/// <summary>
/// A person heir that exists only on paper, identified by name and date of birth.
/// </summary>
public class PappPersonProbateHeir : ProbateHeir
{
    /// <summary>The person's name.</summary>
    [JsonPropertyName("name")]
    public required PersonName Name { get; set; }

    /// <summary>The person's date of birth.</summary>
    [JsonPropertyName("dateOfBirth")]
    public required DateTimeOffset DateOfBirth { get; set; }
}

/// <summary>
/// A person heir identified by their national identity number.
/// </summary>
public class PersonProbateHeir : ProbateHeir
{
    /// <summary>The person's national identity number.</summary>
    [JsonPropertyName("nin")]
    public required string Nin { get; set; }
}
