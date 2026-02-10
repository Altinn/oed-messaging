using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

/// <summary>
/// The eventtype published when updates for an estate are recevied from DA
/// </summary>
public class EstateCaseUpdatedEvent
{
    [JsonPropertyName("caseId")] 
    public required string CaseId { get; set; }
    [JsonPropertyName("caseNumber")] 
    public required string CaseNumber { get; set; }
    [JsonPropertyName("caseStatus")] 
    public required string CaseStatus { get; set; }
    [JsonPropertyName("districtCourtName")]
    public required string DistrictCourtName { get; set; }
    [JsonPropertyName("probateDeadline")] 
    public required DateTimeOffset ProbateDeadline { get; set; }
    [JsonPropertyName("probateResult")] 
    public ProbateResult? ProbateResult { get; set; }
    [JsonPropertyName("probateResultV2")]
    public ProbateResultV2? ProbateResultV2 { get; set; }
    [JsonPropertyName("resultType")]
    public string? ResultType { get; set; }
    [JsonPropertyName("isCancelled")]
    public bool? IsCancelled { get; set; }
    [JsonPropertyName("accessDate")]
    public DateTimeOffset? AccessDate { get; set; }
    [JsonPropertyName("heirRoles")]
    public IEnumerable<HeirRole> HeirRoles { get; set; } = [];
    [JsonPropertyName("heirRolesV2")]
    public IEnumerable<HeirRoleV2> HeirRolesV2 { get; set; } = [];
}

public class ProbateResult
{
    [JsonPropertyName("heirs")]
    public IEnumerable<string> Heirs { get; set; } = [];
    [JsonPropertyName("acceptsDebtHeirs")]
    public IEnumerable<string> AcceptsDebtHeirs { get; set; } = [];
    [JsonPropertyName("result")]
    public required string Result { get; set; }
}

public class ProbateResultV2
{
    [JsonPropertyName("heirs")]
    public IEnumerable<ProbateHeir> Heirs { get; set; } = [];
    [JsonPropertyName("result")]
    public required string Result { get; set; }
}

public class HeirRole
{
    [JsonPropertyName("nin")]
    public string? Nin { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("relation")]
    public string? Relation { get; set; }

    [JsonPropertyName("signedDate")]
    public DateTimeOffset? SignedDate { get; set; }

    [JsonPropertyName("preferredSettlementProcedure")]
    public string? PreferredSettlementProcedure { get; set; }

    [JsonPropertyName("willingToAssumeDebt")]
    public bool WillingToAssumeDebt { get; set; }

    [JsonPropertyName("waiver60DayPeriod")]
    public bool Waiver60DayPeriod { get; set; }

    [JsonPropertyName("probateCertificateRecipient")]
    public bool ProbateCertificateRecipient { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PersonHeirRole), typeDiscriminator: "Person")]
[JsonDerivedType(typeof(PappPersonHeirRole), typeDiscriminator: "PappPerson")]
[JsonDerivedType(typeof(OrganizationHeirRole), typeDiscriminator: "Organization")]
[JsonDerivedType(typeof(PappOrganizationHeirRole), typeDiscriminator: "PappOrganization")]
public abstract class HeirRoleV2
{
    [JsonPropertyName("relation")]
    public required string Relation { get; set; }

    [JsonPropertyName("probateCertificateRecipient")]
    public bool ProbateCertificateRecipient { get; set; }
}

public class PappOrganizationHeirRole : HeirRoleV2
{
    [JsonPropertyName("orgNo")]
    public string? OrgNo { get; set; }

    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class OrganizationHeirRole : HeirRoleV2
{
    [JsonPropertyName("orgNo")]
    public required string OrgNo { get; set; }
}

public class PappPersonHeirRole : HeirRoleV2
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public required DateTimeOffset DateOfBirth { get; set; }
}

public class PersonHeirRole : HeirRoleV2
{
    [JsonPropertyName("nin")]
    public string? Nin { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("signedDate")]
    public DateTimeOffset? SignedDate { get; set; }
}


[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PersonProbateHeir), typeDiscriminator: "Person")]
[JsonDerivedType(typeof(PappPersonProbateHeir), typeDiscriminator: "PappPerson")]
[JsonDerivedType(typeof(OrganizationProbateHeir), typeDiscriminator: "Organization")]
[JsonDerivedType(typeof(PappOrganizationProbateHeir), typeDiscriminator: "PappOrganization")]
public abstract class ProbateHeir
{
    [JsonPropertyName("willingToAssumeDebt")]
    public bool WillingToAssumeDebt { get; set; } = false;
}

public class PappOrganizationProbateHeir : ProbateHeir
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class OrganizationProbateHeir : ProbateHeir
{
    [JsonPropertyName("orgNo")]
    public required string OrgNo { get; set; }
}

public class PappPersonProbateHeir : ProbateHeir
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public required DateTimeOffset DateOfBirth { get; set; }
}

public class PersonProbateHeir : ProbateHeir
{
    [JsonPropertyName("nin")]
    public string? Nin { get; set; }
}