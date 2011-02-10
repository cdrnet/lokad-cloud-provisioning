﻿#region Copyright (c) Lokad 2009-2011
// This code is released under the terms of the new BSD licence.
// URL: http://www.lokad.com/
#endregion

using Microsoft.WindowsAzure.ServiceRuntime;

namespace Lokad.Cloud.Web
{
	public class WebRole : RoleEntryPoint
	{
		public override bool OnStart()
		{
			RoleEnvironment.Changing += (sender, args) => { RoleEnvironment.RequestRecycle(); };

			return base.OnStart();
		}
	}
}
