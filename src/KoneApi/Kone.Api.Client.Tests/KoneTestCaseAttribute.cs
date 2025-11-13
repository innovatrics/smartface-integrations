namespace Kone.Api.Client.Tests
{
    [System.AttributeUsage(AttributeTargets.All, AllowMultiple = true)
    ]
    public class KoneTestCaseAttribute(int caseNumber, string description) : Attribute
    {
        public int CaseNumber { get; } = caseNumber;
        public string Description { get; } = description;
    }
}
