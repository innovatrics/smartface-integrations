public class SupportingData
{
    public long FreefieldDefinitionId { get; set; }
    public long SmartFaceBadgeIdentifierType { get; set; }

    public SupportingData(long freefieldDefinitionId, long smartFaceBadgeIdentifierType)
    {
        this.FreefieldDefinitionId = freefieldDefinitionId;
        this.SmartFaceBadgeIdentifierType = smartFaceBadgeIdentifierType;
    }

}
