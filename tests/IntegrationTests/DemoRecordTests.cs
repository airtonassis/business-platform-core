using Business.Platform.Core.Application.Tenancy;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed partial class DemoRecordTests
{
    [Theory]
    [InlineData("Owner",true)][InlineData("Admin",true)][InlineData("Member",false)][InlineData("PlatformOperator",false)]
    public void Only_owner_and_admin_write(string role,bool allowed)=>Assert.Equal(allowed,DemoRecordRules.CanWrite(role));

    [Theory]
    [InlineData(null,false)][InlineData("",false)][InlineData("   ",false)][InlineData("a",true)]
    public void Title_is_required(string? title,bool valid)=>Assert.Equal(valid,DemoRecordRules.ValidTitle(title));

    [Fact]
    public void Title_length_and_version_binding()
    {
        Assert.True(DemoRecordRules.ValidTitle(new string('a',200)));
        Assert.False(DemoRecordRules.ValidTitle(new string('a',201)));
        var item=new DemoRecord(Guid.NewGuid(),Guid.NewGuid(),"title",1);
        var tag=DemoRecordVersion.Encode(item);
        Assert.True(DemoRecordVersion.TryDecode(tag,item,out var version));Assert.Equal(1,version);
        foreach(var invalid in new[]{"*","W/"+tag,tag+","+tag,tag+"=",DemoRecordVersion.Encode(item with{OrganizationId=Guid.NewGuid()}),DemoRecordVersion.Encode(item with{Id=Guid.NewGuid()})})
            Assert.False(DemoRecordVersion.TryDecode(invalid,item,out _));
    }
}
