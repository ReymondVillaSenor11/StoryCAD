﻿using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using StoryCAD.Models;
using StoryCAD.ViewModels;

namespace StoryCAD.Views;

public sealed partial class ScenePage : BindablePage
{
    public SceneViewModel SceneVm => Ioc.Default.GetService<SceneViewModel>();

    public ScenePage()
    {
        InitializeComponent();
        DataContext = SceneVm;
    }

    private void ScenePurpose_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CheckBox chk = sender as CheckBox;
        StringSelection element = chk.DataContext as StringSelection;
        if (element == null)
            return;
        element.Selection = true;
        SceneVm.OnPropertyChanged(null, null);
    }

    private void ScenePurpose_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CheckBox chk = sender as CheckBox;
        StringSelection element = chk.DataContext as StringSelection;
        if (element == null)
            return;
        element.Selection = false;
        SceneVm.OnPropertyChanged(null, null);
    }

    private void CastMember_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CheckBox chk = sender as CheckBox;
        object item = chk.DataContext;
        if (item == null)
            return;
        StoryElement element = item as StoryElement;
        SceneVm.AddCastMember(element);
    }

    private void CastMember_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CheckBox chk = sender as CheckBox;
        object item = chk.DataContext;
        if (item == null)
            return;
        StoryElement element = item as StoryElement;
        SceneVm.RemoveCastMember(element);
    }
}