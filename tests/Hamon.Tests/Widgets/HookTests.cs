using Hamon.Layout;
using Hamon.Widgets;
using System.Threading.Tasks;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic tests for hooks (UseState/UseFocusNode) and atoms (Watch/UseAtom/Provider).</summary>
public class HookTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private sealed class Capturing : HookWidget
    {
        public Action? OnBuild { get; init; }

        public Action<Hooks>? Use { get; init; }

        public override Widget Build(BuildContext context, Hooks hooks)
        {
            OnBuild?.Invoke();
            Use?.Invoke(hooks);
            return new SizedBox();
        }
    }

    [Fact]
    public void UseState_PersistsAndRebuildsOnlySubtree()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int rootBuilds = 0;
        int hookBuilds = 0;
        HookState<int>? state = null;

        host.SetRoot(() =>
        {
            rootBuilds++;
            return new Column
            {
                Children = new Widget[]
                {
                    new Capturing { OnBuild = () => hookBuilds++, Use = h => state = h.UseState(0) },
                },
            };
        });
        host.Update(Viewport);
        Assert.Equal(1, rootBuilds);
        Assert.Equal(1, hookBuilds);

        state!.Value = 5;
        host.Update(Viewport);

        Assert.Equal(1, rootBuilds);  // 全ツリーは再構築されない
        Assert.Equal(2, hookBuilds);  // この HookWidget だけ再構築
        Assert.Equal(5, state.Value); // 状態は永続
    }

    [Fact]
    public void UseFocusNode_SameInstanceAcrossRebuilds()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var seen = new List<FocusNode>();
        host.SetRoot(() => new Capturing { Use = h => seen.Add(h.UseFocusNode()) });
        host.Update(Viewport);

        host.Invalidate(); // 再構築を強制
        host.Update(Viewport);

        Assert.Equal(2, seen.Count);
        Assert.Same(seen[0], seen[1]); // 同じ FocusNode＝外部メンバ不要で永続
    }

    [Fact]
    public void Atom_Watch_RebuildsOnlyWatchersOnWrite()
    {
        var atom = new Atom<int>(10);
        var host = new HamonRoot(new StubTextRenderer());
        int rootBuilds = 0;
        int watcherBuilds = 0;
        int lastValue = -1;

        host.SetRoot(() =>
        {
            rootBuilds++;
            return new Column
            {
                Children = new Widget[]
                {
                    new Capturing { OnBuild = () => watcherBuilds++, Use = h => lastValue = h.Watch(atom) },
                },
            };
        });
        host.Update(Viewport);
        Assert.Equal(1, rootBuilds);
        Assert.Equal(1, watcherBuilds);
        Assert.Equal(10, lastValue); // 初期値

        host.WriteAtom(atom, 42);
        host.Update(Viewport);

        Assert.Equal(1, rootBuilds);    // 全ツリー不変
        Assert.Equal(2, watcherBuilds); // 購読者だけ再構築
        Assert.Equal(42, lastValue);
    }

    [Fact]
    public void UseAtom_SetterUpdatesValue()
    {
        var atom = new Atom<int>(0);
        var host = new HamonRoot(new StubTextRenderer());
        Action<int>? setter = null;
        int lastValue = -1;

        host.SetRoot(() => new Capturing
        {
            Use = h =>
            {
                (int v, Action<int> set) = h.UseAtom(atom);
                lastValue = v;
                setter = set;
            },
        });
        host.Update(Viewport);
        Assert.Equal(0, lastValue);

        setter!(7);
        host.Update(Viewport);
        Assert.Equal(7, lastValue);
    }

    [Fact]
    public void Atom_WatchedTwiceInBuild_RebuildsOnce()
    {
        var atom = new Atom<int>(1);
        var host = new HamonRoot(new StubTextRenderer());
        int builds = 0;
        host.SetRoot(() => new Capturing
        {
            OnBuild = () => builds++,
            Use = h =>
            {
                h.Watch(atom); // 同一 build で2回 Watch（購読は dedupe される）
                h.Watch(atom);
            },
        });
        host.Update(Viewport);
        Assert.Equal(1, builds);

        host.WriteAtom(atom, 2);
        host.Update(Viewport);
        Assert.Equal(2, builds); // 重複購読でも再構築は1回だけ
    }

    [Fact]
    public void UseEffect_RunsCleanupOnKeyChangeAndUnmount()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int runs = 0;
        int cleanups = 0;
        HookState<int>? state = null;
        host.SetRoot(() => new Capturing
        {
            Use = h =>
            {
                state = h.UseState(0);
                h.UseEffect(
                    () =>
                    {
                        runs++;
                        return () => cleanups++;
                    },
                    state.Value);
            },
        });
        host.Update(Viewport);
        Assert.Equal(1, runs);
        Assert.Equal(0, cleanups);

        state!.Value = 1; // key 変化＝前回クリーンアップ→再実行
        host.Update(Viewport);
        Assert.Equal(2, runs);
        Assert.Equal(1, cleanups);

        host.SetRoot(() => new SizedBox()); // HookWidget を外す＝Unmount でクリーンアップ
        host.Update(Viewport);
        Assert.Equal(2, runs);
        Assert.Equal(2, cleanups);
    }

    [Fact]
    public void StoreProvider_IsolatesAtomFromGlobal()
    {
        var atom = new Atom<int>(0);
        var host = new HamonRoot(new StubTextRenderer());
        int outer = -1;
        int inner = -1;
        Action<int>? setInner = null;

        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                new Capturing { Use = h => outer = h.Watch(atom) },
                new StoreProvider
                {
                    Child = new Capturing
                    {
                        Use = h =>
                        {
                            (int v, Action<int> set) = h.UseAtom(atom);
                            inner = v;
                            setInner = set;
                        },
                    },
                },
            },
        });
        host.Update(Viewport);
        Assert.Equal(0, outer);
        Assert.Equal(0, inner);

        setInner!(5); // 分離ストアのみ更新
        host.Update(Viewport);
        Assert.Equal(5, inner);
        Assert.Equal(0, outer); // グローバルは不変

        host.WriteAtom(atom, 9); // グローバルのみ更新
        host.Update(Viewport);
        Assert.Equal(9, outer);
        Assert.Equal(5, inner); // 分離ストアは不変
    }

    [Fact]
    public void DerivedAtom_RecomputesWhenDependencyChanges()
    {
        var a = new Atom<int>(2);
        var doubled = new Atom<int>(get => get.Get(a) * 2);
        var host = new HamonRoot(new StubTextRenderer());
        int last = -1;
        host.SetRoot(() => new Capturing { Use = h => last = h.Watch(doubled) });

        host.Update(Viewport);
        Assert.Equal(4, last); // 2*2

        host.WriteAtom(a, 5);
        host.Update(Viewport);
        Assert.Equal(10, last); // 依存変化で派生が自動再計算＋購読者再構築
    }

    [Fact]
    public void StoreProvider_InitialValues_Seeds()
    {
        var atom = new Atom<int>(0);
        var host = new HamonRoot(new StubTextRenderer());
        int inner = -1;
        host.SetRoot(() => new StoreProvider
        {
            InitialValues = s => s.Seed(atom, 7),
            Child = new Capturing { Use = h => inner = h.Watch(atom) },
        });
        host.Update(Viewport);
        Assert.Equal(7, inner); // 初期値 0 でなくシード値
    }

    [Fact]
    public void WritableDerived_SetWritesBase()
    {
        var baseA = new Atom<int>(2);
        var doubled = new Atom<int>(g => g.Get(baseA) * 2, (w, v) => w.Set(baseA, v / 2));
        var host = new HamonRoot(new StubTextRenderer());

        Assert.Equal(4, host.ReadAtom(doubled)); // 2*2

        host.WriteAtom(doubled, 10); // 書き込み式で base=5 → 派生再計算
        Assert.Equal(5, host.ReadAtom(baseA));
        Assert.Equal(10, host.ReadAtom(doubled));
    }

    [Fact]
    public void Reset_RestoresInitial()
    {
        var atom = new Atom<int>(0);
        var host = new HamonRoot(new StubTextRenderer());
        host.WriteAtom(atom, 5);
        Assert.Equal(5, host.ReadAtom(atom));

        host.ResetAtom(atom);
        Assert.Equal(0, host.ReadAtom(atom)); // 初期値へ
    }

    [Fact]
    public void AtomFamily_SameAtomPerKey_IndependentValues()
    {
        var family = new AtomFamily<int, int>(k => new Atom<int>(k * 10));
        var host = new HamonRoot(new StubTextRenderer());

        Assert.Same(family[1], family[1]);
        Assert.NotSame(family[1], family[2]);

        host.WriteAtom(family[1], 99);
        Assert.Equal(99, host.ReadAtom(family[1]));
        Assert.Equal(20, host.ReadAtom(family[2])); // 別キーは独立（2*10）
    }

    [Fact]
    public void UseReset_ResetsAndRebuilds()
    {
        var atom = new Atom<int>(0);
        var host = new HamonRoot(new StubTextRenderer());
        int last = -1;
        Action? reset = null;
        host.SetRoot(() => new Capturing
        {
            Use = h =>
            {
                last = h.Watch(atom);
                reset = h.UseReset(atom);
            },
        });
        host.Update(Viewport);

        host.WriteAtom(atom, 5);
        host.Update(Viewport);
        Assert.Equal(5, last);

        reset!();
        host.Update(Viewport);
        Assert.Equal(0, last); // リセットで初期値へ＋再構築
    }

    [Fact]
    public void OnMount_FiresOnFirstSubscriber_CleansUpOnLast()
    {
        int mounted = 0;
        var atom = new Atom<int>(0)
        {
            OnMount = setSelf =>
            {
                mounted++;
                setSelf(7);
                return () => mounted--;
            },
        };
        var host = new HamonRoot(new StubTextRenderer());
        int last = -1;
        host.SetRoot(() => new Capturing { Use = h => last = h.Watch(atom) });

        host.Update(Viewport);
        Assert.Equal(1, mounted);
        Assert.Equal(7, last); // onMount の setSelf が読み取り前に反映

        host.SetRoot(() => new SizedBox()); // 購読者が外れる
        host.Update(Viewport);
        Assert.Equal(0, mounted); // 最後の購読解除でクリーンアップ
    }

    [Fact]
    public void UseAsync_LoadsAndResolves()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AsyncValue<int> seen = AsyncValue<int>.Ok(-1);
        host.SetRoot(() => new Capturing { Use = h => seen = h.UseAsync(() => Task.FromResult(42)) });

        host.Update(Viewport);
        Assert.True(seen.IsLoading); // 初回はローディング

        host.Update(Viewport); // Post をドレイン→Data へ
        Assert.True(seen.HasData);
        Assert.Equal(42, seen.Data);
    }

    [Fact]
    public void UseAsync_SurfacesError()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AsyncValue<int> seen = AsyncValue<int>.Loading;
        host.SetRoot(() => new Capturing { Use = h => seen = h.UseAsync(() => Task.FromException<int>(new InvalidOperationException("boom"))) });

        host.Update(Viewport);
        host.Update(Viewport);

        Assert.True(seen.HasError);
        Assert.IsType<InvalidOperationException>(seen.Error);
    }

    [Fact]
    public void Provider_ScopesAtomForSubtree()
    {
        var atom = new Atom<string>("global");
        var host = new HamonRoot(new StubTextRenderer());
        string outside = string.Empty;
        string inside = string.Empty;

        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                new Capturing { Use = h => outside = h.Watch(atom) },
                new Provider<string>
                {
                    Atom = atom,
                    Value = "scoped",
                    Child = new Capturing { Use = h => inside = h.Watch(atom) },
                },
            },
        });
        host.Update(Viewport);

        Assert.Equal("global", outside); // スコープ外＝グローバル既定（初期値）
        Assert.Equal("scoped", inside);  // Provider 配下＝上書き値
    }
}
