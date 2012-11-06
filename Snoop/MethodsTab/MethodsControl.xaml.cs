// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using Snoop.Infrastructure;
using System.Reflection;
using System.Linq;

using Snoop.Converters;
using System.IO;
using System.Text;

namespace Snoop.MethodsTab
{
    public partial class MethodsControl
    {
        public MethodsControl()
        {
            InitializeComponent();
            DependencyPropertyDescriptor.FromProperty(RootTargetProperty, typeof(MethodsControl)).AddValueChanged(this, RootTargetChanged);

            //DependencyPropertyDescriptor.FromProperty(TargetProperty, typeof(MethodsControl)).AddValueChanged(this, TargetChanged);
            DependencyPropertyDescriptor.FromProperty(ComboBox.SelectedValueProperty, typeof(ComboBox)).AddValueChanged(this.comboBoxMethods, comboBoxMethodChanged);
            DependencyPropertyDescriptor.FromProperty(MethodsControl.IsSelectedProperty, typeof(MethodsControl)).AddValueChanged(this, IsSelectedChanged);

            this._checkBoxUseDataContext.Checked += _checkBoxUseDataContext_Checked;
            this._checkBoxUseDataContext.Unchecked += new RoutedEventHandler(_checkBoxUseDataContext_Unchecked);

            this._checkBoxShowPrivateMethods.Checked += new RoutedEventHandler(_checkBoxShowPrivateMethods_Checked);
            this._checkBoxShowPrivateMethods.Unchecked += new RoutedEventHandler(_checkBoxShowPrivateMethods_Unchecked);

            this._buttonDecompile.Click += DecompileMethodClick;
            //this._buttonOpenFolder.Click += OpenFolderClick;
            this._buttonNavigateILSpy.Click += NavigateILSpyClick;
            this._buttonObjectNavigateILSpy.Click += buttonObjectNavigateILSpy_Click;
            this._buttonInvoke.Click += InvokeMethodClick;
        }

        void _checkBoxShowPrivateMethods_Unchecked(object sender, RoutedEventArgs e)
        {
            PopulateMethodsCombobox(this);
        }

        void _checkBoxShowPrivateMethods_Checked(object sender, RoutedEventArgs e)
        {
            PopulateMethodsCombobox(this);
        }

        void _checkBoxUseDataContext_Unchecked(object sender, RoutedEventArgs e)
        {
            ProcessCheckedProperty();
        }

        private void _checkBoxUseDataContext_Checked(object sender, RoutedEventArgs e)
        {
            ProcessCheckedProperty();
        }

        private void ProcessCheckedProperty()
        {
            if (!this.IsSelected || !this._checkBoxUseDataContext.IsChecked.HasValue || !(this.RootTarget is FrameworkElement))
                return;

            SetTargetToRootTarget();
        }

        private void SetTargetToRootTarget()
        {
            if (this._checkBoxUseDataContext.IsChecked.Value && this.RootTarget is FrameworkElement && ((FrameworkElement)this.RootTarget).DataContext != null)
            {
                this.Target = ((FrameworkElement)this.RootTarget).DataContext;
            }
            else
            {
                this.Target = this.RootTarget;
            }
        }

        private void IsSelectedChanged(object sender, EventArgs args)
        {
            if (this.IsSelected)
            {
                //this.Target = this.RootTarget;
                SetTargetToRootTarget();
            }
        }

        public object RootTarget
        {
            get { return (object)GetValue(RootTargetProperty); }
            set { SetValue(RootTargetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for RootTarget.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootTargetProperty =
            DependencyProperty.Register("RootTarget", typeof(object), typeof(MethodsControl), new UIPropertyMetadata(null));

        private void RootTargetChanged(object sender, EventArgs e)
        {
            if (this.IsSelected)
            {
                this._checkBoxUseDataContext.IsEnabled = (this.RootTarget is FrameworkElement) && ((FrameworkElement)this.RootTarget).DataContext != null;
                this.SetTargetToRootTarget();
            }
        }



        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSelected.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(MethodsControl), new UIPropertyMetadata(false));



        private static void TargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != e.OldValue)
            {
                var methodsControl = (MethodsControl)sender;

                methodsControl.EnableOrDisableDataContextCheckbox();

                PopulateMethodsCombobox(methodsControl);
            }
        }

        private static void PopulateMethodsCombobox(MethodsControl methodsControl)
        {
            var methodInfos = GetMethodInfos(methodsControl.Target, methodsControl._checkBoxShowPrivateMethods.IsChecked.HasValue && methodsControl._checkBoxShowPrivateMethods.IsChecked.Value);
            methodsControl.comboBoxMethods.ItemsSource = methodInfos;

            methodsControl.resultProperties.Visibility = Visibility.Collapsed;
            methodsControl.resultStringContainer.Visibility = Visibility.Collapsed;
            methodsControl.parametersContainer.Visibility = Visibility.Collapsed;

            //SetPreviousSelectedMethod(methodsControl);
            methodsControl.SetPreviousSelectedMethod();
        }

        private void SetPreviousSelectedMethod()
        {

            var currentMethodInfos =
                this.comboBoxMethods.ItemsSource as IList<SnoopMethodInformation>;
            if (currentMethodInfos == null)
                return;

            this.comboBoxMethods.SelectedIndex = -1;

            for (int i = 0; i < currentMethodInfos.Count && this._previousMethodInformation != null; i++)
            {
                var methodInfo = currentMethodInfos[i];
                if (methodInfo.Equals(this._previousMethodInformation))
                {
                    this.comboBoxMethods.SelectedIndex = i;
                    break;
                }
            }
        }

        private void EnableOrDisableDataContextCheckbox()
        {
            if (this._checkBoxUseDataContext.IsChecked.HasValue && this._checkBoxUseDataContext.IsChecked.Value)
                return;

            if (!(this.Target is FrameworkElement) || ((FrameworkElement)this.Target).DataContext == null)
            {
                this._checkBoxUseDataContext.IsEnabled = false;
            }
            else
            {
                this._checkBoxUseDataContext.IsEnabled = true;
            }
        }

        private SnoopMethodInformation _previousMethodInformation = null;
        private void comboBoxMethodChanged(object sender, EventArgs e)
        {
            var selectedMethod = this.comboBoxMethods.SelectedValue as SnoopMethodInformation;
            if (selectedMethod == null || this.Target == null)
                return;

            var parameters = selectedMethod.GetParameters(this.Target.GetType());
            this.itemsControlParameters.ItemsSource = parameters;

            this.parametersContainer.Visibility = parameters.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            this.resultProperties.Visibility = this.resultStringContainer.Visibility = Visibility.Collapsed;

            _previousMethodInformation = selectedMethod;
        }

        public object Target
        {
            get { return (object)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Target.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(object), typeof(MethodsControl), new UIPropertyMetadata(new PropertyChangedCallback(TargetChanged)));

        public void InvokeMethodClick(object sender, RoutedEventArgs e)
        {
            var selectedMethod = this.comboBoxMethods.SelectedValue as SnoopMethodInformation;
            if (selectedMethod == null)
                return;

            SetInvokeVisibilities();

            object[] parameters = new object[this.itemsControlParameters.Items.Count];

            if (!TryToCreateParameters(parameters))
                return;

            TryToInvokeMethod(selectedMethod, parameters);
        }

        private void SetDecompileVisibilities()
        {
            this.resultProperties.Visibility = System.Windows.Visibility.Collapsed;
            this.resultStringContainer.Visibility = System.Windows.Visibility.Collapsed;
            this.textBlockSourceCode.Visibility = System.Windows.Visibility.Visible;
        }

        private void SetInvokeVisibilities()
        {

            this.textBlockSourceCode.Visibility = System.Windows.Visibility.Collapsed;
        }

        private Process ilSpyProcess;

        private void buttonObjectNavigateILSpy_Click(object sender, RoutedEventArgs e)
        {

            var type = this.Target.GetType();
            if (ilSpyProcess == null || ilSpyProcess.HasExited)
            {
                ilSpyProcess = ILSpyInterop.GetOrCreateILSpyProcess(type.Assembly.Location, type.FullName);
            }
            else
            {
                ILSpyInterop.OpenTypeInILSpy(type.Assembly.Location, type.FullName, ilSpyProcess.MainWindowHandle);
            }

        }

        private void NavigateILSpyClick(object sender, RoutedEventArgs e)
        {
            var snoopMethodInfo = this.comboBoxMethods.SelectedValue as SnoopMethodInformation;
            if (snoopMethodInfo == null)
            {
                MessageBox.Show("Please select a method.");
                return;
            }
            var selectedItem = snoopMethodInfo.MethodInfo;

            if (ilSpyProcess == null || ilSpyProcess.HasExited)
            {
                ilSpyProcess = ILSpyInterop.GetOrCreateILSpyProcess(selectedItem.DeclaringType.Assembly.Location, selectedItem.DeclaringType.FullName);
            }
            else
            {
                ILSpyInterop.OpenTypeInILSpy(selectedItem.DeclaringType.Assembly.Location, selectedItem.DeclaringType.FullName, ilSpyProcess.MainWindowHandle);
            }
            //string args = string.Format("ILSpy:\r\n{0}\r\n/navigateTo:T:{1}", selectedItem.DeclaringType.Assembly.Location, selectedItem.DeclaringType.FullName);
            //NativeMethods.Send(ilSpyProcess.MainWindowHandle, args);   
        }

        private void OpenFolderClick(object sender, RoutedEventArgs e)
        {
            //var selectedMethod = this.comboBoxMethods.SelectedValue as SnoopMethodInformation;
            //if (selectedMethod == null)
            //    return;

            //var filePath = selectedMethod.MethodInfo.DeclaringType.Assembly.Location;
            //var argument = @"/select, " + filePath;

            //System.Diagnostics.Process.Start("explorer.exe", argument);
            //ILSPY

            if (ilSpyProcess != null && !ilSpyProcess.HasExited)
                return;

            var location = typeof(Snoop.SnoopUI).Assembly.Location;
            var directory = Path.GetDirectoryName(location);
            directory = Path.Combine(directory, "ILSpy");
            var ilSpyProgram = Path.Combine(directory, "ILSpy.exe");

            var processes = Process.GetProcessesByName("ILSpy");
            if (processes.Length > 0)
            {
                ilSpyProcess = processes[0];
                ilSpyProcess.EnableRaisingEvents = true;
                this._buttonNavigateILSpy.IsEnabled = true;
                ilSpyProcess.Exited += new EventHandler(ilSpyProcess_Exited);
                return;
            }

            ilSpyProcess = new Process();
            ilSpyProcess.StartInfo.FileName = ilSpyProgram;// "ConsoleApplicationDecompile.exe";//decompileProgramName;
            ilSpyProcess.StartInfo.WorkingDirectory = directory;
            ilSpyProcess.EnableRaisingEvents = true;
            ilSpyProcess.Start();
            //ilSpyProcess.Exited += new EventHandler(ilSpyProcess_Exited);
            //this._buttonNavigateILSpy.IsEnabled = true;


        }

        void ilSpyProcess_Exited(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)IlSpyProcessExited);
        }


        private void IlSpyProcessExited()
        {
            ilSpyProcess = null;
            //this._buttonNavigateILSpy.IsEnabled = false;
            //throw new NotImplementedException();
        }

        public void DecompileMethodClick(object sender, RoutedEventArgs e)
        {
            var selectedMethod = this.comboBoxMethods.SelectedValue as SnoopMethodInformation;
            if (selectedMethod == null)
                return;

            SetDecompileVisibilities();

            var source = DecompileMethod(selectedMethod.MethodInfo);
            this.textBlockSourceCode.Text = source;
        }

        public static string DecompileMethod(MethodInfo methodToDecompile)
        {
            if (Environment.Version.Major < 4)
            {
                var sourceCode = DecompileMethodUsingExternalProcess(methodToDecompile);
                return sourceCode;
            }
            else
            {
                var sourceCode = DecompileMethodByLoadingAssembly(methodToDecompile);
                return sourceCode;
            }

        }

        private static MethodInfo _decompileMethodInfo = null;
        private static string DecompileMethodByLoadingAssembly(MethodInfo methodToDecompile)
        {
            if (_decompileMethodInfo == null)
            {
                var location = typeof(Snoop.SnoopUI).Assembly.Location;
                var directory = Path.GetDirectoryName(location);
                directory = Path.Combine(directory, "ILSpy");

                var assembly = Assembly.LoadFrom(Path.Combine(directory, "ConsoleApplicationDecompile.exe"));
                var type = assembly.GetType("ConsoleApplicationDecompile.Program");
                _decompileMethodInfo = type.GetMethod("GetSourceOfMethod");
            }

            try
            {
                var result = _decompileMethodInfo.Invoke(null, new object[] { methodToDecompile });

                return result.ToString();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string GetParametersString(MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();

            if (parameters.Length == 0)
                return string.Empty;

            string[] parametersStringArray = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                parametersStringArray[i] = parameters[i].ParameterType.Name;

            var parametersString = string.Join("|", parametersStringArray);
            return parametersString;
        }

        private static string DecompileMethodUsingExternalProcess(MethodInfo methodToDecompile)
        {
            var location = typeof(Snoop.SnoopUI).Assembly.Location;
            var directory = Path.GetDirectoryName(location);
            directory = Path.Combine(directory, "ILSpy");
            var decompileProgramName = Path.Combine(directory, "ConsoleApplicationDecompile.exe");

            Process decompileProcess;
            decompileProcess = new Process();
            decompileProcess.StartInfo.FileName = decompileProgramName;// "ConsoleApplicationDecompile.exe";//decompileProgramName;
            decompileProcess.StartInfo.WorkingDirectory = directory;
            //// Set UseShellExecute to false for redirection.
            var parametersStringArray = GetParametersString(methodToDecompile);
            //decompileProcess.StartInfo.Arguments = "\"" + methodToDecompile.DeclaringType.Assembly.Location + "\"" + " " + methodToDecompile.DeclaringType.Name + " " + methodToDecompile.Name + " " + parametersStringArray;
            decompileProcess.StartInfo.Arguments = string.Format("\"{0}\" {1} {2} {3}", methodToDecompile.DeclaringType.Assembly.Location,
                methodToDecompile.DeclaringType.Name,
                methodToDecompile.Name,
                parametersStringArray);


            decompileProcess.StartInfo.UseShellExecute = false;

            //// Redirect the standard output of the sort command.   
            decompileProcess.StartInfo.RedirectStandardOutput = true;
            StringBuilder sourceCode = new StringBuilder();
            decompileProcess.Start();
            sourceCode.Append(decompileProcess.StandardOutput.ReadToEnd());
            decompileProcess.WaitForExit();

            return sourceCode.ToString();
        }

        private bool TryToCreateParameters(object[] parameters)
        {
            try
            {
                for (int index = 0; index < this.itemsControlParameters.Items.Count; index++)
                {
                    var paramInfo = this.itemsControlParameters.Items[index] as SnoopParameterInformation;
                    if (paramInfo == null)
                        return false;

                    if (paramInfo.ParameterType.Equals(typeof(DependencyProperty)))
                    {
                        DependencyPropertyNameValuePair valuePair = paramInfo.ParameterValue as DependencyPropertyNameValuePair;
                        parameters[index] = valuePair.DependencyProperty;
                    }
                    //else if (paramInfo.IsCustom || paramInfo.IsEnum)
                    else if (paramInfo.ParameterValue == null || paramInfo.ParameterType.IsAssignableFrom(paramInfo.ParameterValue.GetType()))
                    {
                        parameters[index] = paramInfo.ParameterValue;
                    }
                    else
                    {
                        var converter = TypeDescriptor.GetConverter(paramInfo.ParameterType);
                        parameters[index] = converter.ConvertFrom(paramInfo.ParameterValue);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error creating parameter");
                return false;
            }
        }

        private void TryToInvokeMethod(SnoopMethodInformation selectedMethod, object[] parameters)
        {
            try
            {
                var returnValue = selectedMethod.MethodInfo.Invoke(this.Target, parameters);

                if (returnValue == null)
                {
                    SetNullReturnType(selectedMethod);
                    return;
                }
                else
                {
                    this.resultStringContainer.Visibility = this.textBlockResult.Visibility = this.textBlockResultLabel.Visibility = System.Windows.Visibility.Visible;
                }

                this.textBlockResultLabel.Text = "Result as string: ";
                this.textBlockResult.Text = returnValue.ToString();

                var properties = returnValue.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                //var properties = PropertyInformation.GetAllProperties(returnValue, new Attribute[] { new PropertyFilterAttribute(PropertyFilterOptions.All) });

                if (properties.Length == 0)
                {
                    this.resultProperties.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    this.resultProperties.Visibility = System.Windows.Visibility.Visible;
                    this.propertyInspector.RootTarget = returnValue;
                }
            }
            catch (Exception ex)
            {
                string message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show(message, "Error invoking method");
            }
        }

        private void SetNullReturnType(SnoopMethodInformation selectedMethod)
        {
            if (selectedMethod.MethodInfo.ReturnType == typeof(void))
            {
                this.resultStringContainer.Visibility = this.resultProperties.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                this.resultProperties.Visibility = System.Windows.Visibility.Collapsed;
                this.resultStringContainer.Visibility = System.Windows.Visibility.Visible;
                this.textBlockResult.Text = string.Empty;
                this.textBlockResultLabel.Text = "Method evaluated to null";
                this.textBlockResult.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private static IList<SnoopMethodInformation> GetMethodInfos(object o, bool getPrivateMethods)
        {
            if (o == null)
                return new ObservableCollection<SnoopMethodInformation>();
            Type t = o.GetType();
            var bindingFlags = getPrivateMethods
                                   ? BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod
                                   : BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod;

            var methods = t.GetMethods(bindingFlags);

            var methodsToReturn = new List<SnoopMethodInformation>();

            foreach (var method in methods)
            {
                if (method.IsSpecialName)
                    continue;

                var info = new SnoopMethodInformation(method);
                info.MethodName = method.Name;

                methodsToReturn.Add(info);
            }
            methodsToReturn.Sort();

            return methodsToReturn;
        }

        private void ChangeTarget_Click(object sender, RoutedEventArgs e)
        {
            if (this.RootTarget == null)
                return;

            var paramCreator = new ParameterCreator();
            paramCreator.TextBlockDescription.Text = "Delve into the new desired target by double-clicking on the property. Clicking OK will select the currently delved property to be the new target.";
            paramCreator.Title = "Change Target";
            paramCreator.RootTarget = this.RootTarget;
            paramCreator.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            paramCreator.ShowDialog();

            if (paramCreator.DialogResult.HasValue && paramCreator.DialogResult.Value)
            {
                this.Target = paramCreator.SelectedTarget;
            }
        }

    }

}
