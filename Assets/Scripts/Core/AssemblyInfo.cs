using System.Runtime.CompilerServices;

// ADR-D8 Implementation Guidelines / Risk 表（CS0122 跨 asmdef 不可见, HIGH）：
// Runtime 程序集必须声明 InternalsVisibleTo 指向测试程序集，否则测试代码调用
// internal 测试缝隙 setter（如 SetXForTesting）会报 CS0122——asmdef 的 Assembly
// Definition References 只暴露 public，不授予 internal 访问权。
//
// /test-setup 钦定的 EditMode 测试 asmdef 名为 "EditModeTests"
// （见 Assets/Tests/EditMode/EditModeTests.asmdef 与其内注释）。
[assembly: InternalsVisibleTo("EditModeTests")]
