﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Drizzle.Editor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public LingoViewModel LingoVM { get; } = new();
    }
}
