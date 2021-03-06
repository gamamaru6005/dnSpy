﻿/*
    Copyright (C) 2014-2016 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Threading;
using dnSpy.Contracts.Themes;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace dnSpy.Text.Classification {
	abstract class EditorFormatMapService {
		readonly IThemeService themeService;
		readonly ITextEditorFontSettingsService textEditorFontSettingsService;
		readonly IEditorFormatDefinitionService editorFormatDefinitionService;
		readonly Dictionary<ITextEditorFontSettings, IEditorFormatMap> toCategoryMap;
		readonly List<CategoryEditorFormatMapUpdater> cachedUpdaters;
		readonly Dispatcher dispatcher;

		protected EditorFormatMapService(IThemeService themeService, ITextEditorFontSettingsService textEditorFontSettingsService, IEditorFormatDefinitionService editorFormatDefinitionService) {
			this.themeService = themeService;
			this.textEditorFontSettingsService = textEditorFontSettingsService;
			this.editorFormatDefinitionService = editorFormatDefinitionService;
			toCategoryMap = new Dictionary<ITextEditorFontSettings, IEditorFormatMap>();
			cachedUpdaters = new List<CategoryEditorFormatMapUpdater>();
			dispatcher = Dispatcher.CurrentDispatcher;
		}

		public IEditorFormatMap GetEditorFormatMap(string category) {
			if (category == null)
				throw new ArgumentNullException(nameof(category));
			var textEditorFontSettings = textEditorFontSettingsService.GetSettings(category);
			IEditorFormatMap map;
			if (toCategoryMap.TryGetValue(textEditorFontSettings, out map))
				return map;
			map = new CategoryEditorFormatMap(dispatcher, editorFormatDefinitionService);
			var updater = new CategoryEditorFormatMapUpdater(themeService, textEditorFontSettings, editorFormatDefinitionService, map);
			cachedUpdaters.Add(updater);
			toCategoryMap.Add(textEditorFontSettings, map);
			return map;
		}
	}

	[Export(typeof(IEditorFormatMapService))]
	sealed class EditorFormatMapServiceImpl : EditorFormatMapService, IEditorFormatMapService {
		[ImportingConstructor]
		public EditorFormatMapServiceImpl(IThemeService themeService, ITextEditorFontSettingsService textEditorFontSettingsService, IEditorFormatDefinitionService editorFormatDefinitionService)
			: base(themeService, textEditorFontSettingsService, editorFormatDefinitionService) {
		}

		public IEditorFormatMap GetEditorFormatMap(ITextView view) {
			if (view == null)
				throw new ArgumentNullException(nameof(view));
			return view.Properties.GetOrCreateSingletonProperty(typeof(ViewEditorFormatMap), () => CreateViewEditorFormatMap(view));
		}

		ViewEditorFormatMap CreateViewEditorFormatMap(ITextView textView) {
			textView.Closed += TextView_Closed;
			return new TextViewEditorFormatMap(textView, this);
		}

		void TextView_Closed(object sender, EventArgs e) {
			var textView = (ITextView)sender;
			textView.Closed -= TextView_Closed;
			var map = (ViewEditorFormatMap)textView.Properties[typeof(ViewEditorFormatMap)];
			textView.Properties.RemoveProperty(typeof(ViewEditorFormatMap));
			map.Dispose();
		}
	}
}
