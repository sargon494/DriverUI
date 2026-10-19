using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Agent.Service.Ipc;
using FluentAssertions;

namespace Agent.Service.Tests;

public class NamedPipeSecurityFactoryTests
{
    [Fact]
    public void CreateDefault_Should_Grant_Exactly_The_Expected_Rules()
    {
        var security = NamedPipeSecurityFactory.CreateDefault();

        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .OrderBy(rule => rule.AccessControlType)
            .ThenBy(rule => ((SecurityIdentifier)rule.IdentityReference).Value)
            .ToList();

        rules.Should().HaveCount(3);

        rules.Should().Contain(rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value == NamedPipeSecurityFactory.LocalSystemSid
            && rule.PipeAccessRights == PipeAccessRights.FullControl
            && rule.AccessControlType == AccessControlType.Allow);

        rules.Should().Contain(rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value == NamedPipeSecurityFactory.BuiltinAdministratorsSid
            && rule.PipeAccessRights == PipeAccessRights.FullControl
            && rule.AccessControlType == AccessControlType.Allow);

        rules.Should().Contain(rule =>
            ((SecurityIdentifier)rule.IdentityReference).Value == NamedPipeSecurityFactory.InteractiveSid
            && (rule.PipeAccessRights & PipeAccessRights.ReadWrite) == PipeAccessRights.ReadWrite
            && !rule.PipeAccessRights.HasFlag(PipeAccessRights.ChangePermissions)
            && rule.AccessControlType == AccessControlType.Allow);
    }
}
