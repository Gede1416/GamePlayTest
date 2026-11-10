using UnityEngine;
using System;
using System.Collections.Generic;
using LuaInterface;
using UnityEditor;

using BindType = ToLuaMenu.BindType;
using System.Reflection;

public static class CustomSettings
{
    public static string saveDir = Application.dataPath + "/ThirdParty/ToLua/Generate/";    
    public static string toluaBaseType = Application.dataPath + "/ThirdParty/ToLua/BaseType/";
    public static string baseLuaDir = Application.dataPath + "/ThirdParty/ToLua/Lua/";
    public static string injectionFilesPath = Application.dataPath + "/ThirdParty/ToLua/Injection/";

    //导出时强制做为静态类的类型(注意customTypeList 还要添加这个类型才能导出)
    //unity 有些类作为sealed class, 其实完全等价于静态类
    public static List<Type> staticClassTypes = new List<Type>
    {        
        typeof(UnityEngine.Application),
        typeof(UnityEngine.Time),
        typeof(UnityEngine.Screen),
        typeof(UnityEngine.SleepTimeout),
        typeof(UnityEngine.Input),
        typeof(UnityEngine.Resources),
        typeof(UnityEngine.Physics),
        typeof(UnityEngine.RenderSettings),
        typeof(UnityEngine.QualitySettings),
        typeof(UnityEngine.GL),
        typeof(UnityEngine.Graphics),
    };

    //附加导出委托类型(在导出委托时, customTypeList 中牵扯的委托类型都会导出， 无需写在这里)
    public static DelegateType[] customDelegateList = 
    {        
        _DT(typeof(Action)),                
        _DT(typeof(UnityEngine.Events.UnityAction)),
        _DT(typeof(System.Predicate<int>)),
        _DT(typeof(System.Action<int>)),
        _DT(typeof(System.Comparison<int>)),
        _DT(typeof(System.Func<int, int>)),
    };

    //在这里添加你要导出注册到lua的类型列表
    public static BindType[] customTypeList =
    {
        _GT(typeof(FairyGUI.EventContext)),
        _GT(typeof(FairyGUI.EventDispatcher)),
        _GT(typeof(FairyGUI.EventListener)),
        _GT(typeof(FairyGUI.InputEvent)),
        _GT(typeof(FairyGUI.DisplayObject)),
        _GT(typeof(FairyGUI.Container)),
        _GT(typeof(FairyGUI.Stage)),
        _GT(typeof(FairyGUI.Controller)),
        _GT(typeof(FairyGUI.GObject)),
        _GT(typeof(FairyGUI.GGraph)),
        _GT(typeof(FairyGUI.GGroup)),
        _GT(typeof(FairyGUI.GImage)),
        _GT(typeof(FairyGUI.GLoader)),
        _GT(typeof(FairyGUI.GMovieClip)),
        _GT(typeof(FairyGUI.TextFormat)),
        _GT(typeof(FairyGUI.GTextField)),
        _GT(typeof(FairyGUI.GRichTextField)),
        _GT(typeof(FairyGUI.GTextInput)),
        _GT(typeof(FairyGUI.GComponent)),
        _GT(typeof(FairyGUI.GList)),
        _GT(typeof(FairyGUI.GRoot)),
        _GT(typeof(FairyGUI.GLabel)),
        _GT(typeof(FairyGUI.GButton)),
        _GT(typeof(FairyGUI.GComboBox)),
        _GT(typeof(FairyGUI.GProgressBar)),
        _GT(typeof(FairyGUI.GSlider)),
        _GT(typeof(FairyGUI.PopupMenu)),
        _GT(typeof(FairyGUI.ScrollPane)),
        _GT(typeof(FairyGUI.Transition)),
        _GT(typeof(FairyGUI.UIPackage)),
        _GT(typeof(FairyGUI.Window)),
        _GT(typeof(FairyGUI.GObjectPool)),
        _GT(typeof(FairyGUI.Relations)),
        _GT(typeof(FairyGUI.RelationType)),
        _GT(typeof(FairyGUI.Timers)),
        _GT(typeof(FairyGUI.GTween)),
        _GT(typeof(FairyGUI.GTweener)),
        _GT(typeof(FairyGUI.EaseType)),
        _GT(typeof(FairyGUI.TweenValue)),
        _GT(typeof(FairyGUI.UIObjectFactory)),
        _GT(typeof(UnityEngine.Time)),
    };
    public static List<Type> dynamicList = new List<Type>()
    {
        typeof(MeshRenderer),
#if !UNITY_5_4_OR_NEWER
        typeof(ParticleEmitter),
        typeof(ParticleRenderer),
        typeof(ParticleAnimator),
#endif

        typeof(BoxCollider),
        typeof(MeshCollider),
        typeof(SphereCollider),
        typeof(CharacterController),
        typeof(CapsuleCollider),

        typeof(Animation),
        typeof(AnimationClip),
        typeof(AnimationState),

        typeof(SkinWeights),
        typeof(RenderTexture),
        typeof(Rigidbody),
    };

    //重载函数，相同参数个数，相同位置out参数匹配出问题时, 需要强制匹配解决
    //使用方法参见例子14
    public static List<Type> outList = new List<Type>()
    {
        
    };
        
    //ngui优化，下面的类没有派生类，可以作为sealed class
    public static List<Type> sealedList = new List<Type>()
    {
        /*typeof(Transform),
        typeof(UIRoot),
        typeof(UICamera),
        typeof(UIViewport),
        typeof(UIPanel),
        typeof(UILabel),
        typeof(UIAnchor),
        typeof(UIAtlas),
        typeof(UIFont),
        typeof(UITexture),
        typeof(UISprite),
        typeof(UIGrid),
        typeof(UITable),
        typeof(UIWrapGrid),
        typeof(UIInput),
        typeof(UIScrollView),
        typeof(UIEventListener),
        typeof(UIScrollBar),
        typeof(UICenterOnChild),
        typeof(UIScrollView),        
        typeof(UIButton),
        typeof(UITextList),
        typeof(UIPlayTween),
        typeof(UIDragScrollView),
        typeof(UISpriteAnimation),
        typeof(UIWrapContent),
        typeof(TweenWidth),
        typeof(TweenAlpha),
        typeof(TweenColor),
        typeof(TweenRotation),
        typeof(TweenPosition),
        typeof(TweenScale),
        typeof(TweenHeight),
        typeof(TypewriterEffect),
        typeof(UIToggle),
        typeof(Localization),*/
    };

    public static BindType _GT(Type t)
    {
        return new BindType(t);
    }

    public static DelegateType _DT(Type t)
    {
        return new DelegateType(t);
    }    


    [MenuItem("Lua/Attach Profiler", false, 151)]
    static void AttachProfiler()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("警告", "请在运行时执行此功能", "确定");
            return;
        }

        LuaClient.Instance.AttachProfiler();
    }

    [MenuItem("Lua/Detach Profiler", false, 152)]
    static void DetachProfiler()
    {
        if (!Application.isPlaying)
        {            
            return;
        }

        LuaClient.Instance.DetachProfiler();
    }
}

