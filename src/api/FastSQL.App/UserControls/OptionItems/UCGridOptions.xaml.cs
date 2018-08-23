﻿using Castle.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FastSQL.App.UserControls.OptionItems
{
    /// <summary>
    /// Interaction logic for UCGridOptions.xaml
    /// </summary>
    public partial class UCGridOptions : UserControl
    {
        
        private object _viewModel;
        private Action<string> _onValueUpdated;
        private object _context;

        public UCGridOptions()
        {
            InitializeComponent();
            //_viewModel = new UCGridOptionsViewModel();
            //DataContext = _viewModel;
            DataContextChanged += UCGridOptions_DataContextChanged;
            dgrData.AutoGeneratingColumn += DgrData_AutoGeneratingColumn;
            dgrData.AutoGeneratedColumns += DgrData_AutoGeneratedColumns;
            dgrData.SourceUpdated += DgrData_SourceUpdated;
            dgrData.RowEditEnding += DgrData_RowEditEnding;
            dgrData.CellEditEnding += DgrData_CellEditEnding;
        }

        private void DgrData_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            
        }

        private void DgrData_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
           
        }

        private void DgrData_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            
        }

        private void DgrData_AutoGeneratedColumns(object sender, EventArgs e)
        {
        }

        private void DgrData_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
        }

        public void SetContext(object ctx)
        {
            if (ctx == null)
            {
                return;
            }
            _context = ctx;
            var t = ctx.GetType();
            var sourceTypeProp = t.GetProperty("SourceType");
            var valueProp = t.GetProperty("Value");
            var sourceType = sourceTypeProp.GetValue(ctx, null) as Type;
            var textValue = valueProp.GetValue(ctx, null) as string;

            if (sourceType == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(textValue))
            {
                textValue = $"[]";
            }
            // Make grid data type
            var enumerableType = typeof(List<>);
            var castedType = enumerableType.MakeGenericType(sourceType);
            var gridData = JsonConvert.DeserializeObject(textValue, castedType);

            // Make viewModel
            var viewModelType = typeof(UCGridOptionsViewModel<>);
            var castedViewModelType = viewModelType.MakeGenericType(sourceType);

            // Set data
            var setDataMethod = castedViewModelType.GetMethod("SetData");
            _viewModel = Activator.CreateInstance(castedViewModelType);
            setDataMethod.Invoke(_viewModel, new[] { gridData });

            // updated changed
            var onValueUpdatedMethod = castedViewModelType.GetMethod("OnValueUpdated");
            onValueUpdatedMethod.Invoke(_viewModel, new[] { _onValueUpdated });
            DataContext = _viewModel;
        }

        public void OnValueUpdated(Action<string> onValueUpdated)
        {
            _onValueUpdated = onValueUpdated;
        }

        private void UCGridOptions_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            
        }
    }
}