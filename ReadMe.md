# Shellbent

Hello.


## what it is
Shellbent allows you to change Visual Studio's title bar's text & colour, based off the file-system path of the solution file, and/or the repository information of either git, svn, or versionr source-control systems.

## settings
To use Shellbent, place a file called `.title-bar-none` in your user directory. Shellbent will watch the file and make changes in real time, so you can iterate on your works of art.

## pattern groups
This file contains pattern-groups definining the behaviour of the title bar under various circumstances. These pattern-groups are activated when the associated _filter_ is satisfied. Pattern-groups are allowed to override previously seen pattern-groups, sequentially down the file (a.k.a. last takes precedence).


The sytnax is for defining a pattern-group is:

`pattern-group[`_filters_`]:`<br />
` - (item-opened|solution-opened|document-opened|nothing-opened): `_pattern_


## filters

Filters allow pattern-groups to be limited to certain situations.

**Basic filters** check if various source-control systems are present. This is done by recursing up the folder heirarchy of the solution/document. These basic filters are: `git`, `vsr`, `svn`,
along with `git-branch`, `vsr-branch`

**Regex filters** allow you to match a property against a basic glob regex. There's only `solution` available, which allows you to match against the solution name.

Examples:

```
# only matches against solutions that are called best_solution
pattern-group[solution =~ best_solution]:

# matches against solutions that begin with 'best-', and end with '-dragon'
pattern-group[solution =~ best-*-dragon]:

# matches when the solution 'awkward_dragon' is part of a git repository
pattern-group[git, solution =~ awkward_dragon]:
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