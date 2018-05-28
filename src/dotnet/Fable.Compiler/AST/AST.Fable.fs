namespace rec Fable.AST.Fable

open Fable
open Fable.AST
open Microsoft.FSharp.Compiler.SourceCodeServices
open System

type EnumTypeKind = NumberEnumType | StringEnumType
type FunctionTypeKind = LambdaType of Type | DelegateType of Type list

type Type =
    | MetaType
    | Any
    | Unit
    | Boolean
    | Char
    | String
    | Regex
    | Number of NumberKind
    | EnumType of kind: EnumTypeKind * fullName: string
    | Option of genericArg: Type
    | Tuple of genericArgs: Type list
    | Array of genericArg: Type
    | List of genericArg: Type
    | FunctionType of FunctionTypeKind * returnType: Type
    | GenericParam of name: string
    | ErasedUnion of genericArgs: Type list
    | DeclaredType of FSharpEntity * genericArgs: Type list

type ValueDeclarationInfo =
    { Name: string
      IsPublic: bool
      IsMutable: bool
      HasSpread: bool }

type BaseConstructorInfo =
    { BaseEntityRef: Expr
      BaseConsRef: Expr
      BaseConsArgs: Expr list
      BaseConsHasSpread: bool }

type ImplicitConstructorDeclarationInfo =
    { Name: string
      IsPublic: bool
      HasSpread: bool
      BaseConstructor: BaseConstructorInfo option
      EntityName: string }

type OverrideDeclarationInfo =
    { Name: string
      Kind: ObjectMemberKind
      EntityName: string }

type InterfaceCastDeclarationInfo =
    { Name: string
      IsPublic: bool
      ImplementingType: FSharpEntity
      InterfaceType: FSharpEntity
      /// Name of the casting functions for inherited interfaces
      InheritedInterfaces: string list }

type Declaration =
    | ActionDeclaration of Expr
    | ValueDeclaration of Expr * ValueDeclarationInfo
    | InterfaceCastDeclaration of ObjectMember list * InterfaceCastDeclarationInfo
    | OverrideDeclaration of args: Ident list * body: Expr * OverrideDeclarationInfo
    | ImplicitConstructorDeclaration of args: Ident list * body: Expr * ImplicitConstructorDeclarationInfo

type File(sourcePath, decls, ?usedVarNames, ?dependencies) =
    member __.SourcePath: string = sourcePath
    member __.Declarations: Declaration list = decls
    member __.UsedVarNames: Set<string> = defaultArg usedVarNames Set.empty
    member __.Dependencies: Set<string> = defaultArg dependencies Set.empty

type Ident =
    { Name: string
      Type: Type
      IsMutable: bool
      IsThisArg: bool
      IsCompilerGenerated: bool
      Range: SourceLocation option }

type ImportKind =
    | CoreLib
    | Internal
    | CustomImport

type EnumKind = NumberEnum of Expr | StringEnum of Expr
type NewArrayKind = ArrayValues of Expr list | ArrayAlloc of Expr

type ValueKind =
    | TypeInfo of Type * SourceLocation option // Error messages need location info
    | This of Type
    | Super of Type
    | Null of Type
    | UnitConstant
    | BoolConstant of bool
    | CharConstant of char
    | StringConstant of string
    | NumberConstant of float * NumberKind
    | RegexConstant of source: string * flags: RegexFlag list
    | Enum of EnumKind * enumFullName: string
    | NewOption of value: Expr option * Type
    | NewTuple of Expr list
    | NewArray of NewArrayKind * Type
    | NewList of headAndTail: (Expr * Expr) option * Type
    | NewRecord of Expr list * FSharpEntity * genArgs: Type list
    | NewErasedUnion of Expr * genericArgs: Type list
    | NewUnion of Expr list * FSharpUnionCase * FSharpEntity * genArgs: Type list
    member this.Type =
        match this with
        | TypeInfo _ -> MetaType
        | This t | Super t | Null t -> t
        | UnitConstant -> Unit
        | BoolConstant _ -> Boolean
        | CharConstant _ -> Char
        | StringConstant _ -> String
        | NumberConstant(_,kind) -> Number kind
        | RegexConstant _ -> Regex
        | Enum(kind, fullName) ->
            let kind =
                match kind with
                | NumberEnum _ -> NumberEnumType
                | StringEnum _ -> StringEnumType
            EnumType(kind, fullName)
        | NewOption(_, t) -> Option t
        | NewTuple exprs -> exprs |> List.map (fun e -> e.Type) |> Tuple
        | NewArray(_, t) -> Array t
        | NewList(_, t) -> List t
        | NewRecord(_, ent, genArgs) -> DeclaredType(ent, genArgs)
        | NewErasedUnion(_, genArgs) -> ErasedUnion genArgs
        | NewUnion(_, _, ent, genArgs) -> DeclaredType(ent, genArgs)

type LoopKind =
    | While of guard: Expr * body: Expr
    | For of ident: Ident * start: Expr * limit: Expr * body: Expr * isUp: bool

type FunctionKind =
    | Lambda of arg: Ident
    | Delegate of args: Ident list

type SpreadKind = NoSpread | SeqSpread | TupleSpread

type CallKind =
    | ConstructorCall of Expr
    | StaticCall of Expr
    | InstanceCall of memb: Expr option

type ArgInfo =
  { ThisArg: Expr option
    Args: Expr list
    /// Argument types as defined in the method signature, this may be slightly different to types of actual argument expressions.
    /// E.g.: signature accepts 'a->'b->'c (2-arity) but we pass int->int->int->int (3-arity)
    SignatureArgTypes: Type list option
    Spread: SpreadKind
    IsSiblingConstructorCall: bool }

type CallInfo =
  { CompiledName: string
    /// See ArgIngo.SignatureArgTypes
    SignatureArgTypes: Type list
    Spread: SpreadKind
    DeclaringEntityFullName: string
    GenericArgs: (string * Type) list }

type OperationKind =
    | Call of kind: CallKind * info: ArgInfo
    | CurriedApply of applied: Expr * args: Expr list
    | Emit of macro: string * args: ArgInfo option
    | UnaryOperation of UnaryOperator * Expr
    | BinaryOperation of BinaryOperator * left:Expr * right:Expr
    | LogicalOperation of LogicalOperator * left:Expr * right:Expr

type GetKind =
    | ExprGet of Expr
    | ListHead
    | ListTail
    | OptionValue
    | TupleGet of int
    | UnionTag of FSharpEntity
    | UnionField of FSharpField * FSharpUnionCase * FSharpEntity
    | RecordGet of FSharpField * FSharpEntity

type SetKind =
    | VarSet
    | ExprSet of Expr
    | RecordSet of FSharpField * FSharpEntity

type TestKind =
    | TypeTest of Type
    | ErasedUnionTest of Type
    | OptionTest of isSome: bool
    | ListTest of isCons: bool
    | UnionCaseTest of FSharpUnionCase * FSharpEntity

type ObjectMemberKind =
    | ObjectValue
    | ObjectMethod of hasSpread: bool
    | ObjectGetter
    | ObjectSetter
    | ObjectIterator

type ObjectMember = (* name: *) Expr * (* value: *) Expr * ObjectMemberKind

type OptimizableCastKind =
    | AsSeqFromList
    | AsPojo of Core.CaseRules
    | AsUnit

type Expr =
    | Value of ValueKind
    | IdentExpr of Ident
    /// Defer some casts until last pass to have more opportunities for optimization (e.g. list to seq)
    | OptimizableCast of Expr * OptimizableCastKind * targetType: Type
    | Import of selector: string * path: string * ImportKind * Type

    | Function of FunctionKind * body: Expr * name: string option
    | ObjectExpr of ObjectMember list * Type * baseCall: Expr option

    | Test of Expr * TestKind * range: SourceLocation option
    | Operation of OperationKind * typ: Type * range: SourceLocation option
    | Get of Expr * GetKind * typ: Type * range: SourceLocation option

    | Debugger
    | Throw of Expr * typ: Type * range: SourceLocation option

    | DecisionTree of Expr * targets: (Ident list * Expr) list
    | DecisionTreeSuccess of targetIndex: int * boundValues: Expr list * Type

    | Sequential of Expr list
    | Let of bindings: (Ident * Expr) list * body: Expr
    | Set of Expr * SetKind * value: Expr * range: SourceLocation option
    // TODO: Check if we actually need range for loops
    | Loop of LoopKind * range: SourceLocation option
    | TryCatch of body: Expr * catch: (Ident * Expr) option * finalizer: Expr option
    | IfThenElse of guardExpr: Expr * thenExpr: Expr * elseExpr: Expr

    member this.Type =
        match this with
        | Test _ -> Boolean
        | Value kind -> kind.Type
        | IdentExpr id -> id.Type
        | Import(_,_,_,t) | OptimizableCast(_,_,t) | ObjectExpr(_,t,_)
        | Operation(_,t,_) | Get(_,_,t,_) | Throw(_,t,_) | DecisionTreeSuccess(_,_,t) -> t
        | Debugger | Set _ | Loop _ -> Unit
        | Sequential exprs -> (List.last exprs).Type
        | Let(_,expr) | TryCatch(expr,_,_) | IfThenElse(_,expr,_) | DecisionTree(expr,_) -> expr.Type
        | Function(kind,body,_) ->
            match kind with
            | Lambda arg -> FunctionType(LambdaType arg.Type, body.Type)
            | Delegate args -> FunctionType(DelegateType(args |> List.map (fun a -> a.Type)), body.Type)

    member this.Range: SourceLocation option =
        match this with
        | Value _ | Import _ | OptimizableCast _
        | ObjectExpr _ | Debugger | Sequential _ | Let _
        | IfThenElse _ | TryCatch _ | DecisionTree _ | DecisionTreeSuccess _ -> None

        | Function(_,body,_) -> body.Range
        | IdentExpr id -> id.Range
        | Test(_,_,r) | Operation(_,_,r) | Get(_,_,_,r) | Throw(_,_,r) | Set(_,_,_,r) | Loop(_,r) -> r
