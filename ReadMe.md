# Shellbent

Hello.


## what it is
Shellbent is a Visual Studio plugin that allows you to change the Visual Studio title-bar text & colour. It can react and display information from the file-system path of the solution file, and the repository information of either Git, Svn, Perforce, or Versionr source-control systems.

## settings file
Shellbent is configured through settings files named `.shellbent` in either the user directory, or a parent directory of a Visual Studio solution. Shellbent will respond to changes to the file(s) in realtime, so you may quickly iterate on your works of art. The per-solution settings file takes priority over the user directory settings file. The per-solution file is the first `.shellbent` file found walking up the directory tree from the loaded solution's `.sln` file location.

## settings format
The settings files are written in YAML.

The top-level element is an array of objects that configure Visual Studio. The ordering is important, as the settings are visited in-order until satisfied. Placing a catch-all setting at the bottom makes sense.

The configuration object contains the fields:
 * **predicates**\
 This is a list of strings that determine if the object is applicable for a given situation.\
 \
  A list of predicates is given below.
  
    ``` yaml
    # this configuration object is only applicable when we're in a git repo
    - predicates: [git]

    # this configuration object is only applicable when we're in the master branch
    # this naturally implies the "git" predicate
    - predicates: [git-branch =~ master]

    # combining predicates for very specific behaviour
    - predicates: [solution-name =~ Shellbent, git-branch =~ release]
    ```

 * **title-bar-caption**\
 A string that takes a template that will be used to format the text display in the title-bar of Visual Studio 2017, and modifies the thumbnail caption for both Visual Studio 2017 & 2019. (Visual Studio 2019 does not have a traditional title-bar).\
 \
 The template format is defined further down below.
    ``` yaml
    # this is the standard title-bar template that mimics Visual Studio
      title-bar-caption: $item-name?ide-mode{ $} - $ide-name
    ```
 * **title-bar-background**\
 For Visual Studio 2017 changes the background colour of the title-bar. On 2019 it changes the background colour of the menu-bar that serves as the title-bar.
    ``` yaml
    # a dark blue background
      title-bar-background: "#224"
    ```

 * **title-bar-foreground**\
 For Visual Studio 2017 changes the text of color of the title-bar. Does not affect Visual Studio 2019.

 * **blocks**\
 An array of setting objects that apply only to Visual Studio 2019. These add additional blocks of information to the right of the item-name block that replaced the title-bar for Visual Studio 2019.
    ``` yaml
    # add git information in three separate blocks
    - blocks:
      - text: $git-branch
        foreground: "#cc6"
      - text: $git-sha
        foreground: "#acf"
      - text: $git-commit-time-relative
        foreground: "#4d9" 
    ```
    Result:\
    ![blocks example](docs/git_blocks.png)



## predicates

Predicates allow a configuration to be limited to certain situations.

**Basic filters** \
These check if various source-control systems are present. This is done by recursing up the folder heirarchy of the solution/document. These basic filters are:
 * `git`
 * `vsr`
 * `svn`
 * `p4`

Example
``` yaml
# solution part of a git repo
- predicates: [git]

# solution part of a git repo & a p4 repo
- predicates: [git, p4]
```

**Regex filters**\
Regex filters llow you to match a property against a basic glob regex. This is specified with the regex operator `=~`. The available properties are:
 * `git-branch`
 * `git-sha`
 * `p4-client`
 * `p4-view` *
 * `svn-url`
 * `vsr-branch`
 * `vsr-sha`
 * `solution-name`
 * `solution-path`

Examples:

``` yaml
# only matches against solutions that are called best_solution
pattern-group[solution-name =~ best_solution]:

# matches against solutions that begin with 'best-', and end with '-dragon'
pattern-group[solution-name =~ best-*-dragon]:

# matches when the solution 'awkward_dragon' is part of a git repository
pattern-group[git, solution-name =~ awkward_dragon]:
```

## pattern group directives

Directives tell Title Bar None what to do. Under a pattern-group, several directives can be defined. `solution-opened` applies when a solution is open (surprise!). `document-opened` applies when a document has been opeend in Visual Studio without a solution being open. `item-opened` applies in both cases, and is your go-to thing to change. `color` changes the colour of the Title Bar. 

`nothing-opened` currently doesn't work. Oh well!

## patterns

Finally, a Directive contains the pattern which changes the Visual Studio title bar. These patterns contain text and tags that will get replaced with values. The values of the tags are predetermined by the availability of source-control, or are provided by Visual Studio itself.

**Always Available Tags**

 * `ide-name` - "Microsoft Visual Studio"
 * `ide-mode` - "(Debugging)", "(Running)", or ""
 * `item-name` - The name of the solution, or if no solution present, the document.
 * `path` - This is **special**. Left as just `$path`, it's the full path. As `$path(0, 2)`,
            will act as a function, and return x from the leaf directory (x=0 here), for n 
            (n=2 here) segments. So `$path(0, 2)` for the path `D:\hooray\best\things\mysolution.sln`
            returns `best\things`. `$path(1, 1)` would return `best`.

**git Tags**
 * `git-branch` - The name of the currently active git branch
 * `git-sha` - The SHA of the current commit

**versionr Tags**
 * `vsr-branch` - The name of the currently active versionr branch
 * `vsr-sha` - The SHA of the current versionr version

**SVN Tags**
 * `svn-url` - The url of the SVN repository

Tags are enabled by prefixing with `$`, or `?`. Enclosing scopes are defined with braces. Scopes can be predicated when tags are prefixed with `?`, otherwise the `$` tags will always be attempted to be substituted. When a tag-scope is started with a question-mark, a single dollar-sign can be used instead of spelling out the same tag again.

# examples

```
# here, this pattern group is enabled if we're in a git repository. we always prefix with
# the git branch, then the item-name (provided by Visual Studio), then, *if* the IDE Mode
# is available, we provide a space followed by the ide-mode. Then the IDE name, followed
# by the SHA of the current git commit.
#
# lets break down '?ide-mode{ $}' 
#
# we check to see if ide-mode is available (it's not when you're neither debugging nor running).
# if it *is* available, we bring in everything inside the braces, which is a space followed by
# the tag '$', which is known as ide-mode (because that's the tag that kicked this scope off).

pattern-group[git]:
 - solution-opened: $git-branch - $item-name?ide-mode{ $} - $ide-name [$git-sha]
```

A versionr setup
```
pattern-group[vsr]:
 - item-opened: $vsr-branch/$item-name?ide-mode{ $} - $ide-name, sha=$vsr-sha

```

Because pattern-groups are applied in-order, the following colour directives would augment
the above rules. Here the colour of the title bar will be red, since we're in the production
branch:

```
pattern-group[git-branch =~ production]:
 - color: #f00
```

Here we change the colour to purple when we're opened in a location that contains both versionr and SVN source-control systems (maybe for replication):

```
pattern-group[vsr, svn]:
 - color: #f0f
```