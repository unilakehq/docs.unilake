import { ref, nextTick, onMounted } from 'vue'

const TypesenseDialog = {
  template: `<div class="search-dialog hidden flex bg-black bg-opacity-25 items-center" :class="{ open }" 
       @click="$emit('hide')">
    <div class="dialog absolute w-full flex flex-col bg-white dark:bg-gray-800" style="max-height:70vh;" @click.stop="">
      <div class="p-2 flex flex-col" style="max-height: 70vh;">
        <div class="flex">
          <label class="pt-4 mt-0.5 pl-2" for="docsearch-input">
            <svg class="w-8 h-8 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16l2.879-2.879m0 0a3 3 0 104.243-4.242 3 3 0 00-4.243 4.242zM21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>
          </label>
          <input id="docsearch-input" class="search-input" v-model="query" @keyup="search"
                aria-autocomplete="list" aria-labelledby="docsearch-label" autocomplete="off" autocorrect="off" autocapitalize="off" 
                spellcheck="false" placeholder="Search docs" maxlength="64" type="search" enterkeyhint="go"
                @focus="selectedIndex=1" @blur="selectedIndex=-1" @keydown="onKeyDown">
          <div class="mt-5 mr-3"><button class="search-cancel" @click="$emit('hide')">Cancel</button></div>
        </div>
        <div v-if="results.allItems.length" class="group-results border-0 border-t border-solid border-gray-400 mx-2 pr-1 py-2 overflow-y-scroll" style="max-height:60vh">
          <div v-for="g in results.groups" :key="g.group" class="group-result mb-2">
            <h3 class="m-0 text-lg text-gray-600" v-html="g.group"></h3>
            <div v-for="result in g.items" :key="result.id" :aria-selected="result.id == selectedIndex"
                 class="group-item rounded-lg bg-gray-50 dark:bg-gray-900 mb-1 p-2 flex" @mouseover="onHover(result.id)" @click="go(result.url)">              
              <div class="min-w-min mr-2 flex items-center">
                <svg v-if="result.type=='doc'" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z"></path></svg>
                <svg v-else-if="result.type=='content'" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"></path></svg>
                <svg v-else class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14"></path></svg>
              </div>
              <div class="overflow-hidden">
                <div class="snippet overflow-ellipsis overflow-hidden whitespace-nowrap text-sm" v-html="result.snippetHtml"></div>
                <h4><a class="text-sm text-gray-600" :href="result.url" v-html="result.titleHtml"></a></h4>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>`,
  emits: ['hide'],
  props: { open: Boolean },
  setup(props, { emit }) {
    const results = ref({ groups: [], allItems: [] })
    const query = ref('')

    let lastQuery = ''
    let timeout = null
    function search(txt) {
      if (!query.value) {
        results.value = { groups: [], allItems: [] }
        return
      }
      timeout = setTimeout(() => {
        if (timeout != null) {
          if (lastQuery === query.value) return
          lastQuery = query.value
          clearTimeout(timeout)
          // typesense API reference: https://typesense.org/docs/0.21.0/api/documents.html#search
          fetch(
            'https://search.docs.servicestack.net/collections/typesense_docs/documents/search?q=' +
              encodeURIComponent(query.value) +
              '&query_by=hierarchy.lvl0,hierarchy.lvl1,content,hierarchy.lvl2,hierarchy.lvl3&group_by=hierarchy.lvl0',
            {
              headers: {
                // Search only API key for Typesense.
                'x-typesense-api-key': 'N4N8bF0XwyvzwCGwm3CKB0QcnwyWtygo',
              },
            }
          ).then((res) => {
            res.json().then((data) => {
              selectedIndex.value = 1
              let idx = 0
              const groups = {}
              const meta = { groups: [], allItems: [] }
              //console.log(data)

              data.grouped_hits.forEach((gh) => {
                let groupName = gh.group_key[0]
                meta.groups.push({ group: groupName })
                let group = groups[groupName] ?? (groups[groupName] = [])
                gh.hits.forEach((hit) => {
                  let doc = hit.document
                  let highlight =
                    hit.highlights.length > 0 ? hit.highlights[0] : null
                  let item = {
                    id: ++idx,
                    titleHtml:
                      doc.hierarchy.lvl3 ??
                      doc.hierarchy.lvl2 ??
                      doc.hierarchy.lvl1 ??
                      doc.hierarchy.lvl0,
                    snippetHtml: highlight?.snippet,
                    // search result type for icon
                    type:
                      highlight?.field === 'content' ? 'content' : 'heading',
                    // search results have wrong domain, use relative
                    url: doc.url.substring(
                      doc.url.indexOf('/', 'https://'.length)
                    ),
                  }
                  let titleOnly = stripHtml(item.titleHtml)
                  if (titleOnly === groupName) {
                    item.type = 'doc'
                  }
                  if (titleOnly === stripHtml(item.snippetHtml)) {
                    item.snippetHtml = ''
                  }
                  group.push(item)
                })
              })

              meta.groups.forEach((g) => {
                g.items = groups[g.group] ?? []
                g.items.forEach((item) => {
                  meta.allItems.push(item)
                })
              })

              //console.log(meta)
              results.value = meta
            })
          })
        }
      }, 200)
    }

    let selectedIndex = ref(1)
    /** @param {number} index */
    const onHover = (index) => (selectedIndex.value = index)

    /** @param {string} url */
    function go(url) {
      emit('hide')
      location.href = url
    }

    /** @param {number} id
     *  @param {number} step */
    const next = (id, step) => {
      const meta = results.value
      const pos = meta.allItems.findIndex((x) => x.id === id)
      if (pos === -1) return meta.allItems[0]
      const nextPos = (pos + step) % meta.allItems.length
      return nextPos >= 0
        ? meta.allItems[nextPos]
        : meta.allItems[meta.allItems.length + nextPos]
    }

    let ScrollCounter = 0

    /** @param {KeyboardEvent} e */
    function onKeyDown(e) {
      const meta = results.value
      if (!meta || meta.allItems.length === 0) return
      if (
        e.code === 'ArrowDown' ||
        e.code === 'ArrowUp' ||
        e.code === 'Home' ||
        e.code === 'End'
      ) {
        selectedIndex.value =
          e.code === 'Home'
            ? meta.allItems[0]?.id
            : e.code === 'End'
            ? meta.allItems[meta.allItems.length - 1]?.id
            : next(selectedIndex.value, e.code === 'ArrowUp' ? -1 : 1).id
        nextTick(() => {
          let el = document.querySelector('[aria-selected=true]'),
            elGroup = el?.closest('.group-result'),
            elParent = elGroup?.closest('.group-results')

          ScrollCounter++
          let counter = ScrollCounter

          if (el && elGroup && elParent) {
            if (
              el === elGroup.firstElementChild?.nextElementSibling &&
              elGroup === elParent.firstElementChild
            ) {
              //console.log('scrollTop', 0)
              elParent.scrollTo({ top: 0, left: 0 })
            } else if (
              el === elGroup.lastElementChild &&
              elGroup === elParent.lastElementChild
            ) {
              //console.log('scrollEnd', elParent.scrollHeight)
              elParent.scrollTo({ top: elParent.scrollHeight, left: 0 })
            } else {
              if (typeof IntersectionObserver != 'undefined') {
                let observer = new IntersectionObserver((entries) => {
                  if (entries[0].intersectionRatio <= 0) {
                    //console.log('el.scrollIntoView()', counter, ScrollCounter)
                    if (counter === ScrollCounter) el.scrollIntoView()
                  }
                  observer.disconnect()
                })
                observer.observe(el)
              }
            }
          }
        })
        e.preventDefault()
      } else if (e.code === 'Enter') {
        let match = meta.allItems.find((x) => x.id === selectedIndex.value)
        if (match) {
          go(match.url)
          e.preventDefault()
        }
      }
    }

    /** @param {string} s */
    function stripHtml(s) {
      return s && s.replace(/<[^>]*>?/gm, '')
    }

    return { results, query, selectedIndex, search, onHover, go, onKeyDown }
  },
}

export default {
  components: {
    TypesenseDialog,
  },
  template: `<div>
        <TypesenseDialog :open="openSearch" @hide="hideSearch" />
            <div @click="showSearch" class="relative w-[158px] md:w-[193px] border border-bgNeutarl rounded-lg hover:bg-backgroundFaded hover:border-brand">
              <div class="absolute inset-y-0 flex items-center pointer-events-none start-0 ps-4">
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path fill-rule="evenodd" clip-rule="evenodd" d="M0.833313 7.33334C0.833313 3.74349 3.74346 0.833336 7.33331 0.833336C10.9232 0.833336 13.8333 3.74349 13.8333 7.33334C13.8333 8.94811 13.2439 10.426 12.2695 11.5624L14.3535 13.6464C14.5488 13.8417 14.5488 14.1583 14.3535 14.3536C14.1583 14.5488 13.8417 14.5488 13.6464 14.3536L11.5624 12.2696C10.426 13.244 8.94809 13.8333 7.33331 13.8333C3.74347 13.8333 0.833313 10.9232 0.833313 7.33334ZM11.2224 11.2224C12.2182 10.2266 12.8333 8.85225 12.8333 7.33334C12.8333 4.29577 10.3709 1.83334 7.33331 1.83334C4.29575 1.83334 1.83331 4.29577 1.83331 7.33334C1.83331 10.3709 4.29575 12.8333 7.33331 12.8333C8.85223 12.8333 10.2265 12.2183 11.2224 11.2225C11.2224 11.2225 11.2224 11.2224 11.2224 11.2224Z" fill="#525D61"/>
                </svg>
              </div>
              <button type="submit" id="default-search" class="flex items-center self-stretch w-full px-4 py-2 text-sm bg-transparent border-transparent rounded-lg cursor-pointer peer ps-10 text-bodyText focus:ring-brand focus:border-brand" placeholder="Search" required>
                Search
              </button>
              <button type="submit" class="text-gray-400 absolute end-2.5 bottom-1.5 mr-1 bg-transparent leading-[24px] text-xs peer-focus:text-transparent ">ctrl+k</button>
            </div> 
    </div>
    `,
  setup() {
    const openSearch = ref(false)
    function showSearch() {
      openSearch.value = true
      nextTick(() => {
        /** @@type {HTMLInputElement} */
        const el = document.querySelector('#docsearch-input')
        el?.select()
        el?.focus()
      })
    }
    const hideSearch = () => (openSearch.value = false)
    /** @@param {KeyboardEvent} e */
    function onKeyDown(e) {
      if (e.code === 'Escape') {
        hideSearch()
      } else if (e.target.tagName !== 'INPUT') {
        if (e.code === 'Slash' || (e.ctrlKey && e.code === 'KeyK')) {
          showSearch()
          e.preventDefault()
        }
      }
    }

    onMounted(() => {
      window.addEventListener('keydown', onKeyDown)
    })

    return {
      openSearch,
      showSearch,
      hideSearch,
      onKeyDown,
    }
  },
}
