namespace Game.Data
{
    /// <summary>
    /// 可用物品预留接口（本步无人实现，仅定义备用）。
    /// 后续加工/使用系统实现该接口接入行项的"使用"逻辑。
    /// </summary>
    public interface IUsableItem
    {
        bool CanUse();
        string UseTooltip();   // 不可用时的提示文案
        void Use();
    }
}
