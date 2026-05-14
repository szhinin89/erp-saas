import ZHSearchBar, { type ZHSearchBarProps } from '../shared/ZHSearchBar'

export type SearchBarProps = ZHSearchBarProps

export function SearchBar(props: SearchBarProps) {
  return <ZHSearchBar {...props} />
}
