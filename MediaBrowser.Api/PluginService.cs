﻿using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Serialization;
using MediaBrowser.Controller;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Plugins;
using ServiceStack.ServiceHost;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ServiceStack.Text.Controller;

namespace MediaBrowser.Api
{
    /// <summary>
    /// Class Plugins
    /// </summary>
    [Route("/Plugins", "GET")]
    public class GetPlugins : IReturn<List<PluginInfo>>
    {
    }

    /// <summary>
    /// Class GetPluginAssembly
    /// </summary>
    [Route("/Plugins/{Id}/Assembly", "GET")]
    public class GetPluginAssembly
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Class UninstallPlugin
    /// </summary>
    [Route("/Plugins/{Id}", "DELETE")]
    public class UninstallPlugin : IReturnVoid
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Class GetPluginConfiguration
    /// </summary>
    [Route("/Plugins/{Id}/Configuration", "GET")]
    public class GetPluginConfiguration
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Class UpdatePluginConfiguration
    /// </summary>
    [Route("/Plugins/{Id}/Configuration", "POST")]
    public class UpdatePluginConfiguration : IRequiresRequestStream, IReturnVoid
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }

        /// <summary>
        /// The raw Http Request Input Stream
        /// </summary>
        /// <value>The request stream.</value>
        public Stream RequestStream { get; set; }
    }

    /// <summary>
    /// Class GetPluginConfigurationFile
    /// </summary>
    [Route("/Plugins/{Id}/ConfigurationFile", "GET")]
    public class GetPluginConfigurationFile
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Class GetPluginSecurityInfo
    /// </summary>
    [Route("/Plugins/SecurityInfo", "GET")]
    [Restrict(VisibleLocalhostOnly = true)]
    public class GetPluginSecurityInfo : IReturn<PluginSecurityInfo>
    {
    }

    /// <summary>
    /// Class UpdatePluginSecurityInfo
    /// </summary>
    [Route("/Plugins/SecurityInfo", "GET")]
    public class UpdatePluginSecurityInfo : IReturnVoid, IRequiresRequestStream
    {
        /// <summary>
        /// The raw Http Request Input Stream
        /// </summary>
        /// <value>The request stream.</value>
        public Stream RequestStream { get; set; }
    }

    /// <summary>
    /// Class PluginsService
    /// </summary>
    [Export(typeof(IRestfulService))]
    public class PluginService : BaseRestService
    {
        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetPlugins request)
        {
            var result = Kernel.Plugins.OrderBy(p => p.Name).Select(p => p.GetPluginInfo()).ToList();

            return ToOptimizedResult(result);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetPluginAssembly request)
        {
            var plugin = Kernel.Plugins.First(p => p.UniqueId == request.Id);

            return ToStaticFileResult(plugin.AssemblyFilePath);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetPluginConfiguration request)
        {
            var plugin = Kernel.Plugins.First(p => p.UniqueId == request.Id);

            var dateModified = plugin.ConfigurationDateLastModified;

            var cacheKey = (plugin.Version.ToString() + dateModified.Ticks).GetMD5();

            return ToOptimizedResultUsingCache(cacheKey, dateModified, null, () => plugin.Configuration);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetPluginConfigurationFile request)
        {
            var plugin = Kernel.Plugins.First(p => p.UniqueId == request.Id);

            return ToStaticFileResult(plugin.ConfigurationFilePath);
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>System.Object.</returns>
        public object Get(GetPluginSecurityInfo request)
        {
            var kernel = (Kernel)Kernel;

            var result = new PluginSecurityInfo
            {
                IsMBSupporter = kernel.PluginSecurityManager.IsMBSupporter,
                SupporterKey = kernel.PluginSecurityManager.SupporterKey,
                LegacyKey = kernel.PluginSecurityManager.LegacyKey
            };

            return ToOptimizedResult(result);
        }

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        public void Post(UpdatePluginSecurityInfo request)
        {
            var kernel = (Kernel)Kernel;

            var info = JsonSerializer.DeserializeFromStream<PluginSecurityInfo>(request.RequestStream);

            kernel.PluginSecurityManager.SupporterKey = info.SupporterKey;
            kernel.PluginSecurityManager.LegacyKey = info.LegacyKey;
        }

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        public void Post(UpdatePluginConfiguration request)
        {
            // We need to parse this manually because we told service stack not to with IRequiresRequestStream
            // https://code.google.com/p/servicestack/source/browse/trunk/Common/ServiceStack.Text/ServiceStack.Text/Controller/PathInfo.cs
            var pathInfo = PathInfo.Parse(Request.PathInfo);
            var id = new Guid(pathInfo.GetArgumentValue<string>(1));

            var plugin = Kernel.Plugins.First(p => p.UniqueId == id);

            var configuration = JsonSerializer.DeserializeFromStream(request.RequestStream, plugin.ConfigurationType) as BasePluginConfiguration;

            plugin.UpdateConfiguration(configuration);
        }

        /// <summary>
        /// Deletes the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        public void Delete(UninstallPlugin request)
        {
            var kernel = (Kernel)Kernel;

            var plugin = kernel.Plugins.First(p => p.UniqueId == request.Id);

            kernel.InstallationManager.UninstallPlugin(plugin);
        }
    }
}
