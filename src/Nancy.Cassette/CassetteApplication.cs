﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cassette;
using Cassette.HtmlTemplates;
using Cassette.IO;
using Cassette.Scripts;
using Cassette.Stylesheets;
using Cassette.UI;
using Cassette.Utilities;
using Nancy.Conventions;
using Utility.Logging;

namespace Nancy.Cassette
{
  public class CassetteApplication : CassetteApplicationBase
  {
    private static readonly string PlaceholderTrackerKey = typeof (IPlaceholderTracker).FullName;

    public CassetteApplication(IEnumerable<ICassetteConfiguration> configurations,
                               IDirectory rootDirectory, IDirectory cacheDirectory, IUrlGenerator urlGenerator,
                               bool isOutputOptimized, string version, ILogger logger)
      : base(configurations, rootDirectory, cacheDirectory, urlGenerator, isOutputOptimized, version)
    {
      Logger = logger.GetCurrentClassLogger();
    }

    public Response OnBeforeRequest(NancyContext context)
    {
      //Logger.Info("OnBeforeRequest : {0}", context.Request.Url.Path);

      Context = context;

      IPlaceholderTracker tracker;
      if (HtmlRewritingEnabled)
      {
        tracker = new PlaceholderTracker();
      }
      else
      {
        tracker = new NullPlaceholderTracker();
      }
      Context.Items[PlaceholderTrackerKey] = tracker;

      return null;
    }

    public void OnAfterRequest(NancyContext context)
    {
      //Logger.Info("OnAfterRequest : {0}", context.Request.Url.Path);

      var currentContents = context.Response.Contents;
      context.Response.Contents =
        stream =>
        {
          var currentContentsStream = new MemoryStream();
          currentContents(currentContentsStream);
          currentContentsStream.Position = 0;

          var reader = new StreamReader(currentContentsStream);

          var html = reader.ReadToEnd();

          html = GetPlaceholderTracker().ReplacePlaceholders(html);

          var writer = new StreamWriter(stream);
          writer.Write(html);

          writer.Flush();
        };
    }

    protected override IReferenceBuilder<T> GetOrCreateReferenceBuilder<T>(Func<IReferenceBuilder<T>> create)
    {
      var key = "ReferenceBuilder:" + typeof (T).FullName;
      if (Context.Items.ContainsKey(key))
      {
        return (IReferenceBuilder<T>) Context.Items[key];
      }

      var builder = create();
      Context.Items[key] = builder;
      return builder;
    }

    protected override IPlaceholderTracker GetPlaceholderTracker()
    {
      return (IPlaceholderTracker) Context.Items[PlaceholderTrackerKey];
    }

    public void InstallStaticPaths(NancyConventions conventions)
    {
      var staticPaths = new List<string>();
      staticPaths.AddRange(GetBaseDirectories<ScriptModule>());
      staticPaths.AddRange(GetBaseDirectories<StylesheetModule>());
      staticPaths.AddRange(GetBaseDirectories<HtmlTemplateModule>());

      foreach (var staticPath in staticPaths.Distinct())
      {
        if (!string.IsNullOrEmpty(staticPath))
        {
          conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory(staticPath));
          Logger.Info("InstallStaticPaths : {0}", staticPath);
        }
      }
    }


    public void InstallRoutes(CassetteModule cassetteModule)
    {
      //InstallModuleRoute<ScriptModule>(cassetteModule);
      //InstallModuleRoute<StylesheetModule>(cassetteModule);
      //InstallModuleRoute<HtmlTemplateModule>(cassetteModule);

      //InstallRawFileRoute(cassetteModule);

      //InstallAssetRoute(cassetteModule);
    }

    private IEnumerable<string> GetBaseDirectories<T>()
      where T : Module
    {
      return GetModuleContainer<T>()
        .Modules
        .Where(module => !module.Path.IsUrl())
        .Select(module => module.Path.Split(new[] {'/'})[1]);
    }

    private void InstallStaticPath<T>(NancyConventions conventions)
      where T : Module
    {
      var container = this.GetModuleContainer<T>();
      foreach (var module in container.Modules)
      {
        var staticPath = Nancy.Cassette.UrlGenerator.GetModuleStaticPath(module);
      }
    }


    private void InstallModuleStaticPath<T>(CassetteModule cassetteModule)
      where T : Module
    {
      var url = Nancy.Cassette.UrlGenerator.GetModuleRouteUrl<T>();
      cassetteModule.Get[url] = p => url;

      Logger.Info("InstallModuleRoute : {0}", url);
    }

    private void InstallRawFileRoute(CassetteModule cassetteModule)
    {
      var url = Nancy.Cassette.UrlGenerator.GetRawFileRouteUrl();

      cassetteModule.Get[url] = p => url;

      Logger.Info("InstallRawFileRoute : {0}", url);
    }

    private void InstallAssetRoute(CassetteModule cassetteModule)
    {
      // Used to return compiled coffeescript, less, etc.
      var url = Nancy.Cassette.UrlGenerator.GetAssetRouteUrl();

      cassetteModule.Get[url] = p => url;

      Logger.Info("InstallAssetRoute : {0}", url);
    }

    public NancyContext Context { get; private set; }
    public ILogger Logger { get; private set; }
  }
}