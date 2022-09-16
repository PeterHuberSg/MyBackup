/**************************************************************************************

MyBackup.HelpWindow
===================

Help window displaying some links to further information.

Written in 2022 by Jürgpeter Huber, Singapore 
Contact: https://github.com/PeterHuberSg/MyBackup

To the extent possible under law, the author(s) have dedicated all copyright and 
related and neighboring rights to this software to the public domain worldwide under
the Creative Commons 0 license (details see LICENSE.txt file, see also
<http://creativecommons.org/publicdomain/zero/1.0/>). 

This software is distributed without any warranty. 
**************************************************************************************/


using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace MyBackup {


  /// <summary>
  /// Interaction logic for HelpWindow.xaml
  /// </summary>
  public partial class HelpWindow: Window {


    public HelpWindow() {
      InitializeComponent();

      CodeProjectHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
      GitHubHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
      CreativeCommonHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
    }


    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
      Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) {
        UseShellExecute = true,
      });
      e.Handled = true;
    }
  }
}
