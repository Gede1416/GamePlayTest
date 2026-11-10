using System.Collections.Generic;

namespace ViewSystem
{
    public interface IView
    {
        public void CreatSonBack<T>(T t);// 创建子窗口 排序最后
        public void CreatSonFront();// 创建子窗口 排序最前
        public void Close();
    }

    public abstract class ViewNode : IView
    {
        private List<IView> sonNodes;

        public abstract void CreatSonBack<T>(T t);
        public abstract void CreatSonFront();
        public abstract void Close();
    }
}
