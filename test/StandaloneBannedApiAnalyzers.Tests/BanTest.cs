namespace StandaloneBannedApiAnalyzers.Tests;

public class BanTest
{
    [Theory]
    [InlineData("T:N.BannedType", """
                var o = new N.BannedType();
                """)]
    [InlineData("T:N.BannedType", """
                var v = (new N.BannedType()).BannedMethod();
                """)]
    [InlineData("T:N.BannedType", """
                new N.BannedType().BannedMethod(0);
                """)]
    [InlineData("T:N.BannedType", """
                new N.BannedType().BannedMethod<decimal>(0m);
                """)]
    [InlineData("T:N.BannedType", """
                new N.BannedType().BannedMethod(() => 0);
                """)]
    [InlineData("T:N.BannedType", """
                var v = (new N.BannedType()).BannedField;
                """)]
    [InlineData("T:N.BannedType", """
                var v = (new N.BannedType()).BannedProperty;
                """)]
    [InlineData("T:N.BannedType", """
                var o = new N.BannedType();
                o.BannedEvent += (object sender, EventArgs e) => {};
                """)]
    [InlineData("T:N.BannedType`1", """
                var o = new N.BannedType<int>();
                """)]
    [InlineData("T:N.BannedType", """
                var v = N.BannedType.StaticBannedMethod();
                """)]
    [InlineData("T:N.BannedType", """
                N.BannedType.StaticBannedMethod(0);
                """)]
    [InlineData("T:N.BannedType", """
                N.BannedType.StaticBannedMethod<decimal>(0m);
                """)]
    [InlineData("T:N.BannedType", """
                N.BannedType.StaticBannedMethod(() => 0);
                """)]
    [InlineData("T:N.BannedType", """
                var v = N.BannedType.StaticBannedField;
                """)]
    [InlineData("T:N.BannedType", """
                var v = N.BannedType.StaticBannedProperty;
                """)]
    [InlineData("T:N.BannedType", """
                N.BannedType.StaticBannedEvent += (object sender, EventArgs e) => {};
                """)]
    [InlineData("T:N.BannedType", """
                class A {
                    public N.BannedType Prop { get; set; }
                }
                """)]
    [InlineData("T:N.BannedType", """
                class A {
                    public void Method(N.BannedType arg) {}
                }
                """)]
    [InlineData("T:N.BannedType", """
                class A : N.BannedType {}
                """)]
    [InlineData("T:N.BannedType", """
                [N.Banned]
                class A : N.BannedType {}
                """)]
    [InlineData("N:N", """
                var o = new N.BannedType();
                """)]
    [InlineData("N:N", """
                class A {
                    public N.BannedType Prop { get; set; }
                }
                """)]
    [InlineData("N:N", """
                class A {
                    public void Method(N.BannedType arg) {}
                }
                """)]
    [InlineData("N:N", """
                class A : N.BannedType {}
                """)]
    [InlineData("N:N", """
                [N.Banned]
                class A : N.BannedType {}
                """)]
    [InlineData("N:N", """
                var v = N.BannedType.StaticBannedMethod();
                """)]
    [InlineData("N:N", """
                N.BannedType.StaticBannedMethod(0);
                """)]
    [InlineData("N:N", """
                N.BannedType.StaticBannedMethod<decimal>(0m);
                """)]
    [InlineData("N:N", """
                N.BannedType.StaticBannedMethod(() => 0);
                """)]
    [InlineData("N:N", """
                var v = N.BannedType.StaticBannedField;
                """)]
    [InlineData("N:N", """
                var v = N.BannedType.StaticBannedProperty;
                """)]
    [InlineData("N:N", """
                N.BannedType.StaticBannedEvent += (object sender, EventArgs e) => {};
                """)]
    [InlineData("N:System.Reflection", """
                var m = typeof(N.BannedType).GetMethod("BannedMethod", Type.EmptyTypes);
                m.Invoke(new N.BannedType(), null);
                """)]
    [InlineData("N:System.Reflection", """
                typeof(N.BannedType).GetMethod("BannedMethod", Type.EmptyTypes).Invoke(new N.BannedType(), null);
                """)]
    public async Task ShouldBeBanned(string additionalText, string code)
    {
        var empty = new BannedSymbolsAdditionalText("");
        var bannedSymbols = new BannedSymbolsAdditionalText(additionalText);

        var diagnosticsShouldBeEmpty = await Csx.CompileCodeAsync(code, empty);
        Assert.Empty(diagnosticsShouldBeEmpty);

        var diagnostics = await Csx.CompileCodeAsync(code, bannedSymbols);
        Assert.NotEmpty(diagnostics);
        Assert.Equal("RS0030", diagnostics.First().Id);
    }
}