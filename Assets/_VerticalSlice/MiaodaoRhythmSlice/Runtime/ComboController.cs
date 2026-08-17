// VERTICAL SLICE — NOT FOR PRODUCTION (reference only; never refactor into Assets/Scripts)
// Validation Question: 踩拍苗刀战斗是否「手感即奖励」而非「附加摩擦」（R-FEEL-01 / M0 手感判据）
// Date: 2026-08-16
//
// C2 连招图的【切片替身】：硬编码小型状态图 + 评级门控 + 软重置 + 段数隔离（对齐 C2 规则 5/7/10）。
// 节奏时机已由 RhythmJudge 判定完毕，这里只处理 CancelTier 门控与输入匹配；不重复时机判定。
// Off 模式下 RhythmJudge 已将 CancelTier 封顶 Standard，故 L2→L3adv（Advanced 专属）天然不可达，
// 无需在此特判——这就是规则 10 段数隔离在切片里的落地方式。
using UnityEngine;

namespace JadeRabbit.Slice
{
    [DisallowMultipleComponent]
    public class ComboController : MonoBehaviour
    {
        private enum CancelWindowType { None, Standard, Advanced }

        private readonly struct Edge
        {
            public readonly string From;
            public readonly AttackType Input;
            public readonly string To;
            public readonly CancelWindowType Window;

            public Edge(string from, AttackType input, string to, CancelWindowType window)
            {
                From = from;
                Input = input;
                To = to;
                Window = window;
            }
        }

        public const string RootNodeId = "root";

        private static readonly Edge[] Edges =
        {
            new Edge(RootNodeId, AttackType.Light, "L1", CancelWindowType.None),
            new Edge("L1", AttackType.Light, "L2", CancelWindowType.Standard),
            new Edge("L2", AttackType.Light, "L3adv", CancelWindowType.Advanced),
            new Edge(RootNodeId, AttackType.Heavy, "H1", CancelWindowType.None),
            new Edge("L1", AttackType.Heavy, "H1", CancelWindowType.Standard),
            new Edge("L2", AttackType.Heavy, "H1", CancelWindowType.Standard),
            new Edge(RootNodeId, AttackType.Dash, "dash", CancelWindowType.None),
            new Edge("L1", AttackType.Dash, "dash", CancelWindowType.Standard),
            new Edge("L2", AttackType.Dash, "dash", CancelWindowType.Standard),
            new Edge("dash", AttackType.Light, "L1", CancelWindowType.Standard),
        };

        public string CurrentNodeId { get; private set; } = RootNodeId;

        /// <summary>连招节点推进事件：目标节点 id、是否经由 Advanced 取消窗推进。</summary>
        public event System.Action<string, bool> OnBranchAdvance;

        /// <summary>
        /// 提交一次判定结果，尝试沿连招图推进。
        /// Miss（CancelTier.Closed）恒软重置到 root 且不清连击段数（段数是 RhythmJudge 的域）。
        /// </summary>
        public bool Submit(RhythmJudgment j)
        {
            if (j.CancelTier == CancelTier.Closed)
            {
                CurrentNodeId = RootNodeId;
                return false;
            }

            if (TryAdvance(CurrentNodeId, j))
                return true;

            // 无匹配边、或匹配边门控未过 → 软重置回 root，再尝试从 root 匹配该输入（EC5 语义）。
            CurrentNodeId = RootNodeId;
            return TryAdvance(RootNodeId, j);
        }

        private bool TryAdvance(string from, RhythmJudgment j)
        {
            for (int i = 0; i < Edges.Length; i++)
            {
                Edge edge = Edges[i];
                if (edge.From != from || edge.Input != j.Attack) continue;
                if (!GateAllows(edge.Window, j.CancelTier)) continue;

                CurrentNodeId = edge.To;
                OnBranchAdvance?.Invoke(CurrentNodeId, edge.Window == CancelWindowType.Advanced);
                return true;
            }
            return false;
        }

        private static bool GateAllows(CancelWindowType window, CancelTier tier)
        {
            switch (window)
            {
                case CancelWindowType.Advanced: return tier == CancelTier.Advanced;
                case CancelWindowType.Standard: return tier != CancelTier.Closed;
                case CancelWindowType.None: return tier != CancelTier.Closed;
                default: return false;
            }
        }
    }
}
