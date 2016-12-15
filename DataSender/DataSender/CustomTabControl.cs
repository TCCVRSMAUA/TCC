﻿using System.Windows.Forms;

namespace Ets2SdkClient
{
    public class CustomTabPage : TabPage
    {
        public CustomTabPage(string txt) : base(txt)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}